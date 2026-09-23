from reportlab.lib.pagesizes import A4
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, PageBreak
from reportlab.lib.styles import getSampleStyleSheet
from reportlab.lib.enums import TA_CENTER
from pathlib import Path

output = Path("data/documents")
output.mkdir(parents=True, exist_ok=True)

services = [
    "National ID Renewal",
    "Birth Certificate Request",
    "Family Registration Certificate",
    "Address Change Request",
    "Vehicle License Renewal",
    "Driving License Renewal",
    "Property Registration",
    "Building Permit",
    "Small Business License",
    "Food Establishment License",
    "Tourism Activity Permit",
    "Public Transport Permit",
    "Social Assistance Application",
    "Pension Application",
    "Disability Services Request",
    "Healthcare Service Registration",
    "School Enrollment",
    "University Certificate Request",
    "Educational Transcript Request",
    "Public Housing Application",
    "Utility Connection Request",
    "Water Service Registration",
    "Electricity Service Request",
    "Waste Collection Request",
    "Environmental Permit",
    "Public Event Permit",
    "Complaint Submission",
    "Government Information Request",
    "Employment Registration",
    "Tax Registration"
]

styles = getSampleStyleSheet()
title = styles["Title"]
heading = styles["Heading2"]
body = styles["BodyText"]
title.alignment = TA_CENTER

for index, service in enumerate(services, start=1):
    filename = output / f"government-service-{index:02d}.pdf"

    doc = SimpleDocTemplate(
        str(filename),
        pagesize=A4,
        rightMargin=50,
        leftMargin=50,
        topMargin=50,
        bottomMargin=50
    )

    story = []

    for page in range(1, 7):
        story.append(Paragraph(f"Government Service Guide — {service}", title))
        story.append(Spacer(1, 18))

        sections = [
            ("Service Overview",
             f"The {service} is a government service intended to help citizens complete an official administrative procedure. "
             "This synthetic document describes the service workflow, eligibility conditions, required documents, fees, "
             "processing timelines, responsible authority, and escalation rules."),

            ("Eligibility",
             f"Applicants requesting {service} must satisfy the applicable eligibility conditions. "
             "The applicant should provide accurate information and valid supporting documents. "
             "If the available information is incomplete or contradictory, the officer should request clarification "
             "rather than assume an entitlement."),

            ("Required Documents",
             "Applicants may be required to provide a valid identification document, an application form, "
             "supporting evidence related to the request, and any additional document explicitly required by the service. "
             "Expired or inconsistent documents may require correction before processing."),

            ("Fees",
             "Applicable administrative fees depend on the service category and the current official fee schedule. "
             "A citizen-facing response must not invent a fee when the source material does not specify one."),

            ("Processing Timeline",
             "Processing time depends on document completeness, verification requirements, workload, and the responsible authority. "
             "When an exact timeline is not documented, the response should state that the timeline requires confirmation."),

            ("Procedure",
             f"The normal procedure for {service} consists of submitting the request, validating eligibility, "
             "checking the required documents, recording the application, processing the request, and communicating "
             "the final status to the applicant."),

            ("Officer Guidance",
             "Officers should rely only on the available authoritative evidence. "
             "If a requested obligation, entitlement, fee, document, or deadline cannot be supported by the evidence, "
             "the system should escalate the case for human review instead of generating an unsupported claim."),

            ("Exceptions and Escalation",
             "Cases involving conflicting documents, unclear eligibility, missing legal authority, or ambiguous requirements "
             "should be escalated to the responsible officer. The officer may approve, reject, or edit the drafted response."),

            ("Record Keeping",
             "The request should be associated with an auditable record containing the request, relevant evidence, "
             "processing steps, decision, and final response. Sensitive personal information should not be exposed unnecessarily."),

            ("Citizen Communication",
             "The final response should clearly identify the service, summarize eligibility, list supported requirements, "
             "describe documented fees and timelines, and provide escalation guidance when evidence is insufficient.")
        ]

        for section, text in sections:
            story.append(Paragraph(section, heading))
            story.append(Spacer(1, 6))
            story.append(Paragraph(text, body))
            story.append(Spacer(1, 12))

        story.append(PageBreak())

    doc.build(story)

print(f"Created {len(services)} PDF documents.")
