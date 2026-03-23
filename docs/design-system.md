# Design System Integration

The shared design source for future OpenNist UI applications lives under:

- [/Users/pmtar/Development/Projects/OpenNist/src/design-system/DESIGN.md](/Users/pmtar/Development/Projects/OpenNist/src/design-system/DESIGN.md)
- [/Users/pmtar/Development/Projects/OpenNist/src/design-system/tokens.json](/Users/pmtar/Development/Projects/OpenNist/src/design-system/tokens.json)

Platform-oriented outputs currently provided:

- Web CSS token layer:
  - [/Users/pmtar/Development/Projects/OpenNist/src/design-system/web/open-nist-theme.css](/Users/pmtar/Development/Projects/OpenNist/src/design-system/web/open-nist-theme.css)

## Recommended usage

For the Bun/Vite/React app:

- load `open-nist-theme.css` before component styles
- treat the CSS custom properties as the stable token contract
- keep component styling in the web app, but derive all palette, spacing, and typography values from the shared variables

## Source-of-truth rule

The prose spec in `DESIGN.md` is the design intent.

The token file in `tokens.json` is the implementation source of truth.

The platform files should be treated as generated-style projections, even if they are currently maintained manually.

## Next logical step

Add a small token-generation step so:

- `tokens.json` feeds the web CSS variables
- duplicated token maintenance disappears
