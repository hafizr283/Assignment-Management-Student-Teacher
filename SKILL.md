---
name: simple-website-ux
description: Build, update, review, and verify simple, professional, user-friendly websites and web app interfaces. Use for landing pages, marketing sites, portfolios, CRUD applications, dashboards, admin panels, and database-backed websites, especially when the user asks for a simple, clean, modern, responsive, professional, accessible, easy-to-use interface or says not to bother or confuse users. Apply when improving an existing site, turning a UX checklist into an implementation plan, checking whether frontend data matches the API or database, or deciding whether power-user features such as keyboard shortcuts, sticky headers, advanced tables, and URL-synced state are useful. Cover information hierarchy, navigation, responsive behavior, performance, accessibility, forms, feedback, trust, authentication and permission states, loading/empty/error states, caching, pagination, edge-case data, and implementation verification.
---

# Simple Website UX

Make the interface disappear behind the user's task. Treat simplicity as low friction, clear choices, correct data, predictable behavior, and trustworthy feedback rather than merely having few features.

Add an element or interaction only when it improves task completion, comprehension, efficiency, accessibility, or trust.

## Start With the Existing Product

When updating a website:

1. Inspect the current pages, routes, components, styles, API contracts, validation, and user roles before editing.
2. Identify the site's audience and the primary task for each relevant page.
3. Preserve working behavior, established terminology, and recognizable brand elements unless changing them solves a concrete problem.
4. Reuse the existing stack and component patterns when practical. Avoid adding a large dependency for a small visual improvement.
5. Find shared components and tokens before applying repeated page-level fixes.
6. Separate observed problems from personal taste.

Do not perform an unrelated full redesign when focused improvements can meet the request.

## Prioritize the Work

Fix issues in this order:

1. Broken workflows, incorrect data, authorization leaks, and inaccessible controls.
2. Missing feedback, confusing navigation, data loss, and mobile failures.
3. Slow loading, weak hierarchy, difficult forms, and inconsistent behavior.
4. Visual consistency and polish.
5. Optional delight and power-user features.

For reviews, describe the affected page or component, the user impact, the recommended change, and how to verify it.

## Give Each Page a Clear Job

- State the primary user goal for the page: read, find, compare, create, edit, buy, submit, or contact.
- Give the page one visually dominant primary action when one action is genuinely primary.
- Style secondary and destructive actions according to their importance.
- Keep necessary supporting actions available without making them compete for attention.
- Make the page answer quickly: Where am I? What can I do? What happens next? Why should I trust this?

Allow multiple prominent actions on genuine workspaces or comparison screens when users regularly need them; do not force a landing-page pattern onto an admin tool.

## Keep Navigation Predictable

- Include only useful destinations in primary navigation.
- Make the current location clear through a page title, active item, breadcrumb, or equivalent context.
- Use breadcrumbs for meaningful hierarchies, not merely because a site has several pages.
- Preserve search, filter, sort, tab, and pagination state when users inspect an item and return.
- Make back and cancel behavior predictable and protect unsaved work.
- Use sticky navigation only when persistent access saves meaningful effort; ensure it does not consume excessive mobile space or cover focused content.
- Avoid hiding essential actions exclusively behind hover.

## Build Clear Visual Hierarchy

- Use spacing, alignment, size, weight, and contrast to express relationships and priority.
- Use a restrained color palette, type scale, spacing scale, radius system, and icon family.
- Reserve accent color for important emphasis and interactive states.
- Prefer grouping and whitespace over unnecessary cards, borders, shadows, and gradients.
- Keep content scannable with descriptive headings, concise paragraphs, and recognizable controls.
- Test with realistic long labels, large values, missing values, and user-generated content.

Avoid generic template styling that ignores the product's purpose. A professional interface may be quiet and compact or expressive and spacious depending on its audience.

## Write Useful Content

- Use plain, specific, active language: `Save changes` instead of `Submit` when that is the action.
- Keep terminology consistent between navigation, controls, validation, and confirmation messages.
- Put essential information before promotional or supporting details.
- Use sentence case unless the existing brand system requires otherwise.
- Avoid vague controls such as `Click here`, `Yes`, or `OK` when a specific label is possible.
- Make headings and descriptions useful rather than decorative.

Use readable defaults as guidance, not rigid laws: body text commonly starts around 16 px, body line-height around 1.5, and prose lines around 45-80 characters.

## Design Responsive Behavior

- Start with content priorities at the narrowest supported width, then add space and density as room permits.
- Add breakpoints where content or controls stop working, not at arbitrary device labels.
- Reflow, wrap, collapse, or replace layout patterns instead of shrinking everything.
- Give touch controls comfortable target sizes, generally around 44 by 44 CSS pixels where practical.
- Pair hover enhancements with keyboard focus and touch-capable behavior.
- Prevent fixed elements, dialogs, menus, and virtual keyboards from hiding content or actions.
- Decide deliberately how wide tables behave: reflow into cards only when row relationships remain clear; otherwise use controlled horizontal scrolling, column priority, or a detail view.
- Verify portrait, landscape, high zoom, long content, and common mobile widths.

## Make Forms Easy and Safe

- Ask only for data required for the user's task or a clearly explained business need.
- Keep persistent labels and associate them programmatically with inputs.
- Use correct input types, autocomplete tokens, formats, and mobile keyboards.
- Group related fields and reveal conditional fields only when relevant.
- Validate at a useful time without interrupting users before they can finish typing.
- Place errors next to the affected field, explain how to fix them, preserve entered values, and provide an error summary for long forms.
- Disable repeated submission or otherwise make writes idempotent.
- Show a submitting state, then a clear success or recoverable failure state.
- Confirm destructive or hard-to-reverse actions; prefer undo for easily reversible actions.
- Warn before navigation when meaningful unsaved changes would be lost.

Use autofocus only when focus is predictable, helps the primary task, and will not unexpectedly open a mobile keyboard or disorient assistive-technology users.

## Show Complete Interface States

Design and implement every applicable state:

- initial;
- loading or refreshing;
- populated;
- empty;
- no search or filter results;
- validation failure;
- permission denied;
- authentication expired;
- offline or network failure;
- server failure;
- partial data;
- success after a write;
- conflicting concurrent update.

Do not use a blank region to represent empty or failed data. Explain what happened and give the next useful action, such as retrying, clearing filters, creating the first item, signing in, or contacting support.

Keep stale data visibly distinct when showing it during a refresh or failure. Never present known-stale content as freshly confirmed.

## Verify Database-Backed Data

Treat correctness as a UX requirement. A polished page displaying incorrect, unauthorized, duplicated, missing, or stale data has failed.

### Read paths

- Trace displayed values from the database or source system through the API mapping, client query, state transformation, and rendered component.
- Compare representative UI records with API responses and stored records.
- Verify that server and client filtering, sorting, searching, totals, and pagination use compatible rules.
- Use stable ordering and a deterministic tie-breaker for pagination.
- Check page boundaries for missing or duplicated records, especially after inserts and deletes.
- Distinguish a successful zero-row response from a failed request.
- Verify date, time-zone, currency, numeric, boolean, and enum formatting.
- Enforce authorization on the server; hiding a field or button in the UI is not access control.

### Write paths

- Validate input at appropriate client, API, and database boundaries.
- Verify create, edit, delete, status-change, and bulk actions by reading the record back through the normal user-facing path.
- Invalidate, update, or revalidate relevant caches after writes.
- Roll back optimistic UI when the server rejects a change.
- Prevent duplicate records from retries or double clicks when the operation should be unique.
- Define concurrent-edit behavior with version checks, conflict messages, merge choices, or another explicit policy.
- Make audit-sensitive actions attributable when the product requires it.

### Edge-case data

Test with:

- null, empty, zero, and false values;
- very short and very long text;
- special characters and HTML-like input;
- duplicate or near-duplicate records;
- maximum and negative numbers where applicable;
- dates near day, month, year, daylight-saving, and deadline boundaries;
- non-Latin scripts such as Bengali, including search and sorting;
- deleted or inaccessible related records;
- large result sets and the final partial page.

Use safe test data and read-only queries for production verification unless the user explicitly authorizes mutations.

## Meet Accessibility Requirements

Use WCAG 2.2 AA as the default web target unless the project specifies another standard.

- Prefer semantic HTML and native controls before ARIA.
- Maintain a logical heading structure, reading order, and focus order.
- Make every operation usable by keyboard and keep focus clearly visible.
- Manage focus when dialogs, menus, errors, and dynamically inserted content appear or disappear.
- Provide programmatic labels, descriptions, validation associations, and useful image alternatives.
- Do not use color, position, sound, or motion as the only signal.
- Meet text and non-text contrast requirements in default, hover, focus, disabled, error, and selected states.
- Support zoom, text resizing, reflow, reduced motion, and assistive technologies.
- Announce important asynchronous updates without over-announcing routine changes.

Do not claim accessibility from automated tools or visual review alone. Combine automated checks with keyboard and assistive-technology testing for important workflows.

## Keep Performance Invisible

- Remove unused client code and avoid large dependencies for simple interactions.
- Optimize images, set intrinsic dimensions, and lazy-load non-critical media.
- Load only necessary font families and weights; use resilient fallbacks and avoid invisible text.
- Prevent avoidable layout shifts and long main-thread tasks.
- Avoid request waterfalls and repeated API calls; fetch at the layer that best fits the framework and freshness requirement.
- Use skeletons only when they match the eventual layout; use simple progress feedback for short or unpredictable operations.
- Measure on realistic devices and networks rather than trusting development-machine speed.

Use Core Web Vitals as diagnostic targets when relevant: roughly LCP at or below 2.5 seconds, INP at or below 200 milliseconds, and CLS at or below 0.1 at the 75th percentile. Treat them as evidence, not substitutes for testing the actual task.

## Add Trust and Operational Polish

- Use HTTPS and secure authentication/session behavior.
- Provide a favicon, useful document titles, share metadata when relevant, and a real not-found page.
- Remove dead links, placeholder copy, broken images, console errors, and accidental horizontal scrolling.
- Make ownership, contact, support, privacy, pricing, and policy information easy to find when relevant.
- Explain sensitive data use before collection and avoid requesting information without a clear need.
- Keep timestamps, status labels, and destructive actions unambiguous.
- Preserve familiar branding without sacrificing readability or accessibility.

## Add Power-User Features Only When Earned

Consider keyboard shortcuts, command palettes, sticky table headers, column controls, bulk actions, saved views, dense tables, multi-filter interfaces, and URL-synced state when users repeatedly process substantial data.

Ask:

1. Do users perform this task frequently?
2. Does the feature save measurable time or preserve useful context?
3. Is there a discoverable non-expert path?
4. Can the team implement and test its states reliably?
5. Does it work with keyboard, touch, and assistive technology?

Add the feature when the answers justify its complexity. For hybrid products, apply power-user density only to the repeated-use workspace and keep public or occasional-use pages simpler.

## Verify the Finished Implementation

Do not stop after changing markup or CSS. Run the checks appropriate to the project:

1. Build, type-check, lint, and run relevant automated tests.
2. Exercise the main user journeys and role-specific permissions.
3. Test loading, empty, error, validation, success, expired-session, and retry behavior.
4. Check narrow and wide layouts, keyboard navigation, visible focus, zoom, and reduced motion.
5. Verify browser console and network requests for errors, duplication, and unexpected caching.
6. Compare representative rendered data with the API and database when access is available.
7. Confirm that changes did not break unrelated routes or shared components.

Report what was verified, what could not be verified, and any remaining risk.

## Produce Actionable Results

Adapt the output to the request. Provide only useful artifacts, such as:

- a prioritized audit with evidence and fixes;
- a focused implementation plan;
- updated components and styles;
- a page hierarchy or user flow;
- a state and edge-case matrix;
- responsive and accessibility acceptance criteria;
- a frontend/API/database verification checklist;
- a concise test report.

Keep recommendations specific to the actual website. Explain meaningful tradeoffs and skip requirements that add no value for its users.
