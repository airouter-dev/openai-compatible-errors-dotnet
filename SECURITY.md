# Security policy

## Supported versions

Only the latest published minor release receives security fixes. The project is pre-1.0, so users should review changelog entries before upgrading.

## Report a vulnerability privately

Use GitHub's **Report a vulnerability** form in the repository Security tab. Do not open a public issue for a suspected vulnerability.

Include the affected version, a minimal synthetic reproduction, the security impact, and any suggested mitigation. Remove API keys, authorization headers, production prompts, response bodies, customer identifiers, and other sensitive data.

We will acknowledge a complete report, reproduce it, prepare a fix, and coordinate disclosure through the private advisory. Response timing depends on severity and reproducibility; this document does not promise a fixed SLA.

## Scope

Security-relevant areas include failure classification that incorrectly authorizes replay, retention of response or exception content, unbounded metadata, concurrency errors in stream evidence, package provenance, and dependency confusion.

Questions about normal behavior and feature requests belong in public issues and should also use synthetic data.
