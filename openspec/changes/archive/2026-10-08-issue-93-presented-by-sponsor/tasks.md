## 1. Sponsor placement projection

- [x] 1.1 Expose the existing singular `sponsorPlacement` on the public tournament model that the list projection returns, so list surfaces receive it from the current API shape.
- [x] 1.2 Remove the duplicate placement member from the extended detail model and mirror the placement in the mock tournament list projection.

## 2. Presented-by attribution

- [x] 2.1 Add one shared presented-by label that returns nothing for a missing placement or blank sponsor name and otherwise formats the existing `Feature.tournaments.partnerHeading` resource with the sponsor name.
- [x] 2.2 Show that label with the tournament detail hero title, the tournaments overview card title, and the home featured lead and row titles.
- [x] 2.3 Add scoped styling so a long attribution wraps on mobile and desktop without overflowing, and leave the tournament name, page title, and breadcrumb unchanged.

## 3. Validation

- [x] 3.1 Add focused regression coverage for sponsored, unsponsored, blank, and long sponsor names plus the list projection carrying the placement.
- [x] 3.2 Strictly validate the OpenSpec change before code and again after the task list is synchronized.
- [x] 3.3 Run the focused contract test suite and build the web project.
