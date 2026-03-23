# Design System Specification: Technical Precision Editorial

## 1. Overview & Creative North Star
**The Creative North Star: "The Digital Blueprint"**

This design system moves away from the "clunky enterprise tool" aesthetic, instead adopting the sophisticated clarity of high-end engineering schematics and modern architectural journals. We are not just building a library; we are building an authoritative standard for data integrity.

By leveraging **intentional asymmetry** and **high-contrast typography scales**, we break the traditional "grid-lock" of technical software. The interface should feel like a high-performance engine: every element has a functional purpose, but the assembly is elegant, spacious, and undeniably premium. We favor breathability over density, and tonal depth over structural lines.

---

## 2. Colors & Surface Logic

The palette is rooted in deep, authoritative blues and high-performance teals, balanced by a sophisticated neutral scale that prevents the UI from feeling "heavy."

### The "No-Line" Rule
To achieve a signature, high-end look, **1px solid borders for sectioning are strictly prohibited.**
* **Definition through Tonality:** Define structural boundaries solely through background color shifts. For example, a main content area using `surface` should be offset by a sidebar or header using `surface-container-low`.
* **The "Glass & Gradient" Rule:** Use `surface-tint` at 5-10% opacity with a `backdrop-blur` (12px-20px) for floating navigation or overlays. This "Glassmorphism" ensures the UI feels like a cohesive environment rather than a series of disconnected boxes.
* **Signature Textures:** Apply subtle linear gradients (e.g., `primary` to `primary-container`) on hero backgrounds and primary CTAs. This adds a "machined" finish that flat hex codes cannot replicate.

| Token | Hex | Role |
| :--- | :--- | :--- |
| `primary` | #001e40 | Brand Authority (Deep Space Blue) |
| `secondary` | #006a6a | Data Integrity (Teal) |
| `tertiary-fixed` | #ffdcc3 | Technical Highlight (Warning Orange) |
| `surface` | #f8f9ff | The Foundation |
| `surface-container-highest` | #d5e3fc | Deepest Nesting Layer |

---

## 3. Typography
The typographic system creates an "Editorial-Technical" hybrid. We use **Space Grotesk** for high-level expression and **Inter** for functional density.

* **Display & Headline (Space Grotesk):** These are our "Architectural" weights. Use `display-lg` (3.5rem) with tight letter-spacing (-0.02em) to create a sense of engineering precision in headers.
* **Body & Labels (Inter):** Reserved for high-readability tasks. `body-md` (0.875rem) is the workhorse for technical documentation.
* **Monospace Integration:** For hex dumps, file headers, and NIST data structures, use a refined monospace (e.g., JetBrains Mono) matched to the `label-md` scale. It should always appear on a `surface-container-high` background to denote its "Data" status.

---

## 4. Elevation & Depth

### The Layering Principle
Hierarchy is achieved through **Tonal Stacking**.
1. **Level 0 (Base):** `surface`
2. **Level 1 (Sections):** `surface-container-low`
3. **Level 2 (Cards/Modules):** `surface-container-lowest` (White) — this creates a "natural lift" without artificial shadows.

### Ambient Shadows & Ghost Borders
* **Ambient Shadows:** Use only for floating modals. Values: `box-shadow: 0 24px 48px -12px rgba(13, 28, 46, 0.08);`. The shadow color must be a tint of `on-surface` (#0d1c2e), never pure black.
* **The "Ghost Border":** If a separator is required for accessibility, use `outline-variant` at **15% opacity**. A solid, 100% opaque border is a failure of the system.

---

## 5. Components

### Buttons
* **Primary:** `primary` background with a subtle top-down gradient. 0.5rem (`lg`) rounding.
* **Secondary:** No fill. `primary` text with a "Ghost Border" that only reveals itself fully on hover.
* **Tertiary:** `on-surface-variant` text. Use for low-priority actions like "Cancel" or "Clear Filter."

### Data Cards & Lists
* **Anti-Divider Policy:** Never use horizontal rules. Separate list items using the `spacing.4` (0.9rem) scale or by alternating background colors between `surface-container-low` and `surface-container-lowest`.

* **Technical Chips:** Use `secondary-container` for active data states. Shapes should be `md` (0.375rem) rounding to maintain the "Technical Modern" vibe.

### Input Fields
* **Focus State:** Instead of a thick border, use a 2px outer glow of `primary-fixed` and shift the background to `surface-bright`.
* **Monospace Inputs:** Any field requiring file paths or NIST identifiers must use the monospace font-family.

### Custom Component: The "Pixel-Grid" Overlay
For image processing sections (WSQ/JPEG2000), use a repeating 8px background dot pattern in `outline-variant` (10% opacity) to subtly reference the pixel-level precision of the library.

---

## 6. Do's and Don'ts

### Do:
* **Embrace Asymmetry:** Align high-level headlines to the left while keeping technical data blocks centered or right-aligned to create visual interest.
* **Use Generous White Space:** Use the `24` (5.5rem) spacing token for major section margins to signal "High-End" quality.
* **Color as Information:** Use `tertiary` (Orange) sparingly—only for true warnings or "Critical Data" points.

### Don't:
* **No Heavy Dropshadows:** Avoid the "floating card" look popularized by generic Material Design. Stick to tonal layering.
* **No Rounded Corners > 12px:** We are an engineering tool, not a social app. Keep rounding to the `md` (6px) or `lg` (8px) tokens.
* **No Pure Black:** Always use `on-surface` (#0d1c2e) for text to maintain a professional, soft-contrast readability.
