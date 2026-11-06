# ADR 0001: Product model, pricing, and data boundaries

- Status: Accepted
- Date: 2026-11-07

## Context

MoneyBrain needs a clear product model that supports users who want to run the application themselves and users who prefer a managed service. The hosted subscription must not put core budgeting or bookkeeping features behind a paywall, and deployment boundaries must preserve the product's data-ownership promise.

## Decisions

- Offer both free self-hosted software under AGPL-3.0 and a paid MoneyBrain Cloud subscription.
- Keep all core finance features available in both editions.
- Store MoneyBrain Cloud data only on the MoneyBrain-hosted server. Store self-hosted data only on the user's own server. Do not synchronize data between those deployments.
- Price MoneyBrain Cloud at $3.99 per month or $39.99 per year, billed as recurring subscriptions.
- Let subscribers manage and cancel their subscription at any time through Stripe's customer portal.
- Charge for managed hosting, backups, and convenience rather than restricting core finance features.
- Launch with Self-Hosted and MoneyBrain Cloud. MoneyBrain Family is a future plan at $6.99 per month or $69.99 per year and is not available at launch.
- Use the existing Stripe subscription, licensing, customer portal, and webhook implementation. Keep self-hosted licensing disabled by default; configure hosted Stripe credentials and Price IDs in deployment settings, not source-controlled files.

## Consequences

- The pricing page presents Self-Hosted and MoneyBrain Cloud as the launch plans and identifies Family as a future offering.
- Hosted checkout depends on configured Stripe monthly and annual Price IDs, while self-hosted operation does not require Stripe.
- MoneyBrain Cloud customers rely on the hosted service's data retention and backup policies; those policies must be documented before public launch.
- Users who want their data only on their own infrastructure must choose Self-Hosted. There is no cross-deployment sync service.

## Open questions

- Whether MoneyBrain Cloud will offer a free hosted tier is undecided.
- Hosted retention, backup cadence, and restore guarantees need to be specified before launch.
