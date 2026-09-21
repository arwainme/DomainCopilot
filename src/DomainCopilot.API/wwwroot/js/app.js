"use strict";

const state = {
    token: localStorage.getItem("domainCopilotToken"),
    username: localStorage.getItem("domainCopilotUsername"),
    role: localStorage.getItem("domainCopilotRole"),
    latestRunId: localStorage.getItem("domainCopilotLatestRunId")
};

const $ = (selector) => document.querySelector(selector);

const elements = {
    loginView: $("#loginView"),
    appView: $("#appView"),
    loginForm: $("#loginForm"),
    loginMessage: $("#loginMessage"),
    globalMessage: $("#globalMessage"),
    currentUser: $("#currentUser"),
    userRoleBadge: $("#userRoleBadge"),
    dashboardRole: $("#dashboardRole"),
    dashboardHealth: $("#dashboardHealth"),
    dashboardRun: $("#dashboardRun"),
    healthText: $("#healthText"),
    logoutBtn: $("#logoutBtn"),
    pageTitle: $("#pageTitle"),

    startAskBtn: $("#startAskBtn"),
    askForm: $("#askForm"),
    situation: $("#situation"),
    clearAskBtn: $("#clearAskBtn"),
    executeWorkflowBtn: $("#executeWorkflowBtn"),

    workflowLoading: $("#workflowLoading"),
    workflowLoadingText: $("#workflowLoadingText"),
    workflowResult: $("#workflowResult"),
    workflowStatus: $("#workflowStatus"),
    draftResponse: $("#draftResponse"),
    citationsContainer: $("#citationsContainer"),

    approvalPanel: $("#approvalPanel"),
    editedDraft: $("#editedDraft"),
    approveBtn: $("#approveBtn"),
    rejectBtn: $("#rejectBtn"),
    rejectReasonContainer: $("#rejectReasonContainer"),
    rejectReason: $("#rejectReason"),
    confirmRejectBtn: $("#confirmRejectBtn"),

    runForm: $("#runForm"),
    runIdInput: $("#runIdInput"),
    runDetailsPanel: $("#runDetailsPanel"),
    runDetailsTitle: $("#runDetailsTitle"),
    runDetailsStatus: $("#runDetailsStatus"),
    runDetailsJson: $("#runDetailsJson"),

    replayForm: $("#replayForm"),
    replayRunId: $("#replayRunId"),
    replayResult: $("#replayResult"),
    replayRunIdResult: $("#replayRunIdResult"),

    toolsGrid: $("#toolsGrid"),

    usageRequests: $("#usageRequests"),
    usagePromptTokens: $("#usagePromptTokens"),
    usageCompletionTokens: $("#usageCompletionTokens"),
    usageTotalTokens: $("#usageTotalTokens"),
    usageDetails: $("#usageDetails"),
    usageJson: $("#usageJson")
};

document.addEventListener("DOMContentLoaded", initialize);

function initialize() {
    setupNavigation();
    setupEvents();

    if (state.token) {
        showApplication();
        initializeApplication();
    } else {
        showLogin();
    }
}

function setupEvents() {
    elements.loginForm.addEventListener("submit", handleLogin);
    elements.logoutBtn.addEventListener("click", logout);

    elements.startAskBtn.addEventListener("click", () => {
        activateSection("askSection");
        elements.situation.focus();
    });

    elements.askForm.addEventListener("submit", executeWorkflow);

    elements.clearAskBtn.addEventListener("click", () => {
        elements.situation.value = "";
        hideWorkflowResult();
        clearMessage();
    });

    elements.runForm.addEventListener("submit", loadRun);
    elements.replayForm.addEventListener("submit", replayRun);

    elements.approveBtn.addEventListener("click", approveRun);

    elements.rejectBtn.addEventListener("click", () => {
        elements.rejectReasonContainer.classList.toggle("hidden");
    });

    elements.confirmRejectBtn.addEventListener("click", rejectRun);
}

function setupNavigation() {
    document.querySelectorAll(".nav-item").forEach((button) => {
        button.addEventListener("click", () => {
            activateSection(button.dataset.section);
        });
    });
}

function activateSection(sectionId) {
    document.querySelectorAll(".nav-item").forEach((button) => {
        button.classList.toggle(
            "active",
            button.dataset.section === sectionId
        );
    });

    document.querySelectorAll(".content-section").forEach((section) => {
        section.classList.toggle(
            "active-section",
            section.id === sectionId
        );
    });

    const titles = {
        dashboardSection: "Dashboard",
        askSection: "Ask Service",
        runsSection: "Run Details",
        replaySection: "Replay",
        toolsSection: "Tools",
        usageSection: "Usage"
    };

    elements.pageTitle.textContent = titles[sectionId] || "Domain Copilot";

    if (sectionId === "toolsSection" && state.token) {
        loadTools();
    }

    if (sectionId === "usageSection" && state.role === "Officer") {
        loadUsage();
    }
}

async function handleLogin(event) {
    event.preventDefault();

    const username = $("#username").value.trim();
    const password = $("#password").value;

    setButtonLoading(
        elements.loginForm.querySelector("button[type='submit']"),
        true,
        "Signing in..."
    );

    hideMessage(elements.loginMessage);

    try {
        const response = await fetch("/api/auth/login", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                username,
                password
            })
        });

        const data = await readResponse(response);

        if (!response.ok) {
            throw new Error(
                getApiError(data) || "Invalid username or password."
            );
        }

        state.token =
            data.accessToken ||
            data.AccessToken ||
            data.token ||
            data.Token;

        state.username =
            data.username ||
            data.Username ||
            username;

        state.role =
            data.role ||
            data.Role ||
            "Citizen";

        if (!state.token) {
            throw new Error("Login succeeded but no access token was returned.");
        }

        localStorage.setItem("domainCopilotToken", state.token);
        localStorage.setItem("domainCopilotUsername", state.username);
        localStorage.setItem("domainCopilotRole", state.role);

        showMessage(
            elements.loginMessage,
            `Welcome, ${state.username}.`,
            "success"
        );

        setTimeout(() => {
            showApplication();
            initializeApplication();
        }, 300);
    } catch (error) {
        showMessage(
            elements.loginMessage,
            error.message || "Login failed.",
            "error"
        );
    } finally {
        setButtonLoading(
            elements.loginForm.querySelector("button[type='submit']"),
            false,
            "Sign in"
        );
    }
}

function showLogin() {
    elements.loginView.classList.remove("hidden");
    elements.appView.classList.add("hidden");
}

function showApplication() {
    elements.loginView.classList.add("hidden");
    elements.appView.classList.remove("hidden");

    elements.currentUser.textContent =
        state.username || "User";

    elements.userRoleBadge.textContent =
        state.role || "Citizen";

    elements.dashboardRole.textContent =
        state.role || "Citizen";

    document.querySelectorAll(".officer-only").forEach((element) => {
        element.classList.toggle(
            "hidden",
            state.role !== "Officer"
        );
    });

    activateSection("dashboardSection");
}

async function initializeApplication() {
    await checkHealth();

    if (state.latestRunId) {
        elements.dashboardRun.textContent = shortenId(state.latestRunId);
        elements.runIdInput.value = state.latestRunId;
        elements.replayRunId.value = state.latestRunId;
    }

    loadTools();
}

function logout() {
    localStorage.removeItem("domainCopilotToken");
    localStorage.removeItem("domainCopilotUsername");
    localStorage.removeItem("domainCopilotRole");

    state.token = null;
    state.username = null;
    state.role = null;

    hideWorkflowResult();
    clearMessage();

    elements.loginForm.reset();
    showLogin();
}

async function checkHealth() {
    try {
        const response = await fetch("/health");

        if (!response.ok) {
            throw new Error("Health endpoint returned an error.");
        }

        const data = await readResponse(response);

        const status =
            data?.status ||
            data?.Status ||
            "healthy";

        elements.healthText.textContent = status;
        elements.dashboardHealth.textContent = status;
    } catch {
        elements.healthText.textContent = "Unavailable";
        elements.dashboardHealth.textContent = "Unavailable";
    }
}

async function executeWorkflow(event) {
    event.preventDefault();

    const situation = elements.situation.value.trim();

    if (!situation) {
        showGlobalMessage(
            "Please describe the citizen's situation first.",
            "error"
        );
        return;
    }

    hideWorkflowResult();
    clearMessage();

    elements.workflowLoading.classList.remove("hidden");
    elements.workflowLoadingText.textContent =
        "Running eligibility, procedure and response agents...";

    setButtonLoading(
        elements.executeWorkflowBtn,
        true,
        "Executing..."
    );

    try {
        const response = await apiFetch(
            "/api/workflows/government/execute",
            {
                method: "POST",
                body: JSON.stringify({
                    situation
                })
            }
        );

        const data = await readResponse(response);

        if (!response.ok) {
            throw new Error(
                getApiError(data) || "Workflow execution failed."
            );
        }

        renderWorkflowResult(data);

        showGlobalMessage(
            "Government workflow completed.",
            "success"
        );
    } catch (error) {
        showGlobalMessage(
            error.message || "Workflow execution failed.",
            "error"
        );
    } finally {
        elements.workflowLoading.classList.add("hidden");

        setButtonLoading(
            elements.executeWorkflowBtn,
            false,
            "Execute workflow"
        );
    }
}

function renderWorkflowResult(data) {
    elements.workflowResult.classList.remove("hidden");

    const draft = data?.draft || data?.Draft || null;

    const runId =
        data?.runId ||
        data?.RunId ||
        null;

    const status =
        data?.status ||
        data?.Status ||
        "Completed";

    const responseText =
        draft?.responseText ||
        draft?.ResponseText ||
        "";

    const citations =
        draft?.citations ||
        draft?.Citations ||
        [];

    elements.workflowStatus.textContent = status;

    if (responseText) {
        elements.draftResponse.textContent = responseText;
    } else {
        elements.draftResponse.textContent =
            "No draft response was returned by the workflow.";
    }

    renderCitations(citations, data);

    if (runId) {
        saveLatestRunId(runId);

        elements.dashboardRun.textContent =
            shortenId(runId);

        elements.runIdInput.value = runId;
        elements.replayRunId.value = runId;

        loadRunById(runId);
    }

    if (
        state.role === "Officer" &&
        runId &&
        responseText
    ) {
        elements.approvalPanel.classList.remove("hidden");
        elements.editedDraft.value = responseText;
    } else {
        elements.approvalPanel.classList.add("hidden");
    }
}

function renderCitations(citations, workflowData) {
    elements.citationsContainer.innerHTML = "";

    if (!Array.isArray(citations) || citations.length === 0) {
        const empty = document.createElement("p");
        empty.className = "muted";
        empty.textContent =
            "No supporting citations were returned.";
        elements.citationsContainer.appendChild(empty);
        return;
    }

    const evidence = [
        ...(workflowData?.eligibility?.evidence || []),
        ...(workflowData?.procedure?.evidence || [])
    ];

    const evidenceByChunkId = new Map(
        evidence
            .filter(x => x?.chunkId)
            .map(x => [String(x.chunkId), x])
    );

    citations.forEach((citation, index) => {
        const chunkId =
            citation?.chunkId ||
            citation?.ChunkId ||
            "";

        const matchingEvidence =
            evidenceByChunkId.get(String(chunkId));

        const title =
            citation?.documentTitle ||
            citation?.DocumentTitle ||
            matchingEvidence?.documentTitle ||
            matchingEvidence?.DocumentTitle ||
            `Evidence ${index + 1}`;

        const documentId =
            citation?.documentId ||
            citation?.DocumentId ||
            matchingEvidence?.documentId ||
            matchingEvidence?.DocumentId ||
            "—";

        const location =
            citation?.pageNumber ||
            citation?.PageNumber ||
            matchingEvidence?.pageNumber ||
            matchingEvidence?.PageNumber ||
            "—";

        const content =
            matchingEvidence?.content ||
            matchingEvidence?.Content ||
            "Supporting evidence retrieved from the document.";

        const card = document.createElement("article");
        card.className = "citation-card";

        const strong = document.createElement("strong");
        strong.textContent = title;

        const paragraph = document.createElement("p");
        paragraph.textContent = content;

        const source = document.createElement("span");
        source.className = "citation-source";
        source.textContent =
            `Document ID: ${documentId}`;

        const chunk = document.createElement("span");
        chunk.className = "citation-source";
        chunk.textContent =
            `Chunk ID: ${chunkId || "—"}`;

        const locationElement = document.createElement("span");
        locationElement.className = "citation-source";
        locationElement.textContent =
            `Location: ${location}`;

        card.appendChild(strong);
        card.appendChild(paragraph);
        card.appendChild(source);
        card.appendChild(chunk);
        card.appendChild(locationElement);

        elements.citationsContainer.appendChild(card);
    });
}

async function approveRun() {
    const runId = state.latestRunId;

    if (!runId) {
        showGlobalMessage(
            "There is no run available for approval.",
            "error"
        );
        return;
    }

    setButtonLoading(
        elements.approveBtn,
        true,
        "Approving..."
    );

    try {
        const response = await apiFetch(
            `/api/runs/${encodeURIComponent(runId)}/approve`,
            {
                method: "POST"
            }
        );

        const data = await readResponse(response);

        if (!response.ok) {
            throw new Error(
                getApiError(data) || "Approval failed."
            );
        }

        showGlobalMessage(
            "Run approved successfully.",
            "success"
        );

        await loadRunById(runId);
    } catch (error) {
        showGlobalMessage(
            error.message || "Approval failed.",
            "error"
        );
    } finally {
        setButtonLoading(
            elements.approveBtn,
            false,
            "Approve"
        );
    }
}

async function rejectRun() {
    const runId = state.latestRunId;
    const reason = elements.rejectReason.value.trim();

    if (!runId) {
        showGlobalMessage(
            "There is no run available for rejection.",
            "error"
        );
        return;
    }

    if (!reason) {
        showGlobalMessage(
            "Please provide a rejection reason.",
            "error"
        );
        return;
    }

    setButtonLoading(
        elements.confirmRejectBtn,
        true,
        "Rejecting..."
    );

    try {
        const response = await apiFetch(
            `/api/runs/${encodeURIComponent(runId)}/reject`,
            {
                method: "POST",
                body: JSON.stringify({
                    reason
                })
            }
        );

        const data = await readResponse(response);

        if (!response.ok) {
            throw new Error(
                getApiError(data) || "Rejection failed."
            );
        }

        showGlobalMessage(
            "Run rejected successfully.",
            "success"
        );

        elements.rejectReason.value = "";
        elements.rejectReasonContainer.classList.add("hidden");

        await loadRunById(runId);
    } catch (error) {
        showGlobalMessage(
            error.message || "Rejection failed.",
            "error"
        );
    } finally {
        setButtonLoading(
            elements.confirmRejectBtn,
            false,
            "Confirm rejection"
        );
    }
}

async function loadRun(event) {
    event.preventDefault();

    const runId = elements.runIdInput.value.trim();

    if (!runId) {
        showGlobalMessage(
            "Please enter a run ID.",
            "error"
        );
        return;
    }

    await loadRunById(runId);
}

async function loadRunById(runId) {
    try {
        const response = await apiFetch(
            `/api/runs/${encodeURIComponent(runId)}`,
            {
                method: "GET"
            }
        );

        const data = await readResponse(response);

        if (!response.ok) {
            throw new Error(
                getApiError(data) || "Run was not found."
            );
        }

        elements.runDetailsPanel.classList.remove("hidden");
        elements.runDetailsTitle.textContent =
            runId;

        const status =
            findValue(data, [
                "status",
                "Status",
                "runStatus",
                "RunStatus"
            ]) || "—";

        elements.runDetailsStatus.textContent =
            status;

        elements.runDetailsJson.textContent =
            JSON.stringify(data, null, 2);

        elements.runIdInput.value = runId;
    } catch (error) {
        elements.runDetailsPanel.classList.add("hidden");

        showGlobalMessage(
            error.message || "Could not load run.",
            "error"
        );
    }
}

async function replayRun(event) {
    event.preventDefault();

    const runId = elements.replayRunId.value.trim();

    if (!runId) {
        showGlobalMessage(
            "Please enter the original run ID.",
            "error"
        );
        return;
    }

    const submitButton =
        elements.replayForm.querySelector("button[type='submit']");

    setButtonLoading(
        submitButton,
        true,
        "Replaying..."
    );

    try {
        const response = await apiFetch(
            `/api/runs/${encodeURIComponent(runId)}/replay`,
            {
                method: "POST"
            }
        );

        const data = await readResponse(response);

        if (!response.ok) {
            throw new Error(
                getApiError(data) || "Replay failed."
            );
        }

        const replayedRun =
            findNestedObject(
                data,
                ["run", "Run"]
            ) || data;

        const newRunId =
            findValue(
                replayedRun,
                ["runId", "RunId", "id", "Id"]
            );

        elements.replayResult.classList.remove("hidden");

        elements.replayRunIdResult.textContent =
            newRunId || "Created successfully";

        if (newRunId) {
            saveLatestRunId(newRunId);
            elements.dashboardRun.textContent =
                shortenId(newRunId);
        }

        showGlobalMessage(
            "Run replayed successfully.",
            "success"
        );
    } catch (error) {
        showGlobalMessage(
            error.message || "Replay failed.",
            "error"
        );
    } finally {
        setButtonLoading(
            submitButton,
            false,
            "Replay run"
        );
    }
}

async function loadTools() {
    if (!state.token) {
        return;
    }

    try {
        const response = await apiFetch(
            "/api/tools",
            {
                method: "GET"
            }
        );

        const data = await readResponse(response);

        if (!response.ok) {
            throw new Error(
                getApiError(data) || "Could not load tools."
            );
        }

        const tools =
            Array.isArray(data)
                ? data
                : findCollection(
                    data,
                    [
                        "tools",
                        "Tools",
                        "items",
                        "Items"
                    ]
                );

        renderTools(tools || []);
    } catch (error) {
        elements.toolsGrid.innerHTML = "";

        const panel = document.createElement("div");
        panel.className = "panel";

        const message = document.createElement("p");
        message.className = "muted";
        message.textContent =
            error.message || "Could not load tools.";

        panel.appendChild(message);
        elements.toolsGrid.appendChild(panel);
    }
}

function renderTools(tools) {
    elements.toolsGrid.innerHTML = "";

    if (!Array.isArray(tools) || tools.length === 0) {
        const panel = document.createElement("div");
        panel.className = "panel";

        const message = document.createElement("p");
        message.className = "muted";
        message.textContent = "No tools returned by the API.";

        panel.appendChild(message);
        elements.toolsGrid.appendChild(panel);
        return;
    }

    tools.forEach((tool, index) => {
        const card = document.createElement("article");
        card.className = "tool-card";

        const name =
            typeof tool === "string"
                ? tool
                : tool?.name ||
                  tool?.Name ||
                  tool?.toolName ||
                  tool?.ToolName ||
                  `Tool ${index + 1}`;

        const description =
            typeof tool === "string"
                ? "Registered server-side agent tool."
                : tool?.description ||
                  tool?.Description ||
                  "Registered server-side agent tool.";

        const heading = document.createElement("h3");
        heading.textContent = name;

        const paragraph = document.createElement("p");
        paragraph.textContent = description;

        card.appendChild(heading);
        card.appendChild(paragraph);

        elements.toolsGrid.appendChild(card);
    });
}

async function loadUsage() {
    if (state.role !== "Officer") {
        return;
    }

    try {
        const response = await apiFetch(
            "/api/usage",
            {
                method: "GET"
            }
        );

        const data = await readResponse(response);

        if (!response.ok) {
            throw new Error(
                getApiError(data) || "Could not load usage."
            );
        }

        elements.usageDetails.classList.remove("hidden");
        elements.usageJson.textContent =
            JSON.stringify(data, null, 2);

        elements.usageRequests.textContent =
            findNumber(data, [
                "requests",
                "Requests",
                "requestCount",
                "RequestCount"
            ]);

        elements.usagePromptTokens.textContent =
            findNumber(data, [
                "promptTokens",
                "PromptTokens",
                "inputTokens",
                "InputTokens"
            ]);

        elements.usageCompletionTokens.textContent =
            findNumber(data, [
                "completionTokens",
                "CompletionTokens",
                "outputTokens",
                "OutputTokens"
            ]);

        elements.usageTotalTokens.textContent =
            findNumber(data, [
                "totalTokens",
                "TotalTokens"
            ]);
    } catch (error) {
        elements.usageDetails.classList.remove("hidden");
        elements.usageJson.textContent =
            error.message || "Could not load usage.";
    }
}

async function apiFetch(url, options = {}) {
    const headers = new Headers(options.headers || {});

    headers.set("Accept", "application/json");

    if (options.body && !headers.has("Content-Type")) {
        headers.set("Content-Type", "application/json");
    }

    if (state.token) {
        headers.set(
            "Authorization",
            `Bearer ${state.token}`
        );
    }

    return fetch(url, {
        ...options,
        headers
    });
}

async function readResponse(response) {
    const contentType =
        response.headers.get("content-type") || "";

    if (contentType.includes("application/json")) {
        try {
            return await response.json();
        } catch {
            return {};
        }
    }

    const text = await response.text();

    if (!text) {
        return {};
    }

    try {
        return JSON.parse(text);
    } catch {
        return {
            message: text
        };
    }
}

function getApiError(data) {
    return (
        data?.message ||
        data?.Message ||
        data?.error ||
        data?.Error ||
        data?.title ||
        data?.Title ||
        data?.detail ||
        data?.Detail ||
        null
    );
}

function findValue(object, keys) {
    if (!object || typeof object !== "object") {
        return null;
    }

    for (const key of keys) {
        if (
            Object.prototype.hasOwnProperty.call(object, key) &&
            object[key] !== null &&
            object[key] !== undefined
        ) {
            return object[key];
        }
    }

    for (const value of Object.values(object)) {
        if (value && typeof value === "object") {
            const result = findValue(value, keys);

            if (
                result !== null &&
                result !== undefined
            ) {
                return result;
            }
        }
    }

    return null;
}

function findNestedObject(object, keys) {
    if (!object || typeof object !== "object") {
        return null;
    }

    for (const key of keys) {
        if (
            object[key] &&
            typeof object[key] === "object"
        ) {
            return object[key];
        }
    }

    for (const value of Object.values(object)) {
        if (value && typeof value === "object") {
            const result =
                findNestedObject(value, keys);

            if (result) {
                return result;
            }
        }
    }

    return null;
}

function findCollection(object, keys) {
    if (Array.isArray(object)) {
        return object;
    }

    if (!object || typeof object !== "object") {
        return null;
    }

    for (const key of keys) {
        if (Array.isArray(object[key])) {
            return object[key];
        }
    }

    for (const value of Object.values(object)) {
        if (value && typeof value === "object") {
            const result =
                findCollection(value, keys);

            if (Array.isArray(result)) {
                return result;
            }
        }
    }

    return null;
}

function findDraftText(data) {
    const direct =
        findValue(data, [
            "draft",
            "Draft",
            "draftResponse",
            "DraftResponse",
            "response",
            "Response",
            "answer",
            "Answer",
            "text",
            "Text"
        ]);

    if (typeof direct === "string") {
        return direct;
    }

    if (direct && typeof direct === "object") {
        const nested =
            findValue(direct, [
                "responseText",
                "ResponseText",
                "content",
                "Content",
                "text",
                "Text",
                "response",
                "Response"
            ]);

        if (typeof nested === "string") {
            return nested;
        }
    }

    return null;
}

function findNumber(data, keys) {
    const value = findValue(data, keys);

    if (
        value === null ||
        value === undefined ||
        value === ""
    ) {
        return "0";
    }

    const number =
        Number(value);

    return Number.isFinite(number)
        ? number.toLocaleString()
        : String(value);
}

function saveLatestRunId(runId) {
    if (!runId) {
        return;
    }

    state.latestRunId = String(runId);

    localStorage.setItem(
        "domainCopilotLatestRunId",
        state.latestRunId
    );
}

function shortenId(value) {
    if (!value) {
        return "—";
    }

    const text = String(value);

    if (text.length <= 18) {
        return text;
    }

    return `${text.slice(0, 8)}…${text.slice(-6)}`;
}

function setButtonLoading(button, loading, text) {
    if (!button) {
        return;
    }

    if (loading) {
        button.dataset.originalText =
            button.textContent;

        button.textContent =
            text;

        button.disabled = true;
    } else {
        button.textContent =
            text ||
            button.dataset.originalText ||
            button.textContent;

        button.disabled = false;
    }
}

function showMessage(element, message, type) {
    element.textContent = message;
    element.className =
        `message ${type || "info"}`;
}

function hideMessage(element) {
    element.classList.add("hidden");
    element.textContent = "";
}

function showGlobalMessage(message, type) {
    elements.globalMessage.classList.remove("hidden");

    showMessage(
        elements.globalMessage,
        message,
        type
    );

    window.clearTimeout(
        window.__domainCopilotMessageTimer
    );

    window.__domainCopilotMessageTimer =
        window.setTimeout(() => {
            hideMessage(elements.globalMessage);
        }, 5000);
}

function clearMessage() {
    hideMessage(elements.globalMessage);
}

function hideWorkflowResult() {
    elements.workflowResult.classList.add("hidden");
    elements.approvalPanel.classList.add("hidden");
    elements.runDetailsPanel.classList.add("hidden");
}


