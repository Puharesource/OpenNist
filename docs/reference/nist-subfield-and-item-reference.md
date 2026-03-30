# Reference: NIST Subfield and Item Reference

This page documents how OpenNist parses subfields and items inside ANSI/NIST-style textual fields.[^nist-draft][^nist-program]

It is focused on the OpenNist object model and browser inspector behavior rather than reproducing every profile-specific subfield schema from the full ANSI/NIST standard. Where this page describes repeated fields such as `CNT` and `MIN`, it is using the official ANSI/NIST terminology as the reference point and then explaining how OpenNist materializes that structure in code and in the browser.[^nist-draft]

## Parsing model

OpenNist treats a field value as a hierarchy:

- field
- one or more subfields
- one or more items inside each subfield

The separators are:

| Level | Separator | ASCII code | Meaning |
| --- | --- | --- | --- |
| Field terminator | file-group separator | `0x1D` | Ends a field in a fielded record |
| Subfield separator | record separator | `0x1E` | Splits a field into subfields |
| Item separator | unit separator | `0x1F` | Splits one subfield into items |

In OpenNist:

- `NistField.Value` keeps the original joined field value
- `NistField.Subfields` lazily parses the value into structured subfields
- the browser NIST explorer shows subfields only when a field actually contains multiple subfields or multiple items

## Generic rules

### Single-value fields

Most simple fields are stored as:

- one subfield
- one item

Examples:

- `1.002 VER`
- `1.004 TOT`
- `14.006 HLL`

### Repeated-group fields

Some fields are structured as repeated subfield groups. In those cases:

- each subfield represents one logical grouped entry
- each item inside the subfield represents one value inside that entry

Examples:

- `1.003 CNT`
- `9.010 MIN`

### Multi-item fields

Some fields may contain multiple items inside a single subfield. OpenNist preserves those items and exposes them as parsed item lists, even when the field is intentionally treated as positional data rather than as a named item schema.

## Field-specific subfield patterns

### `1.003 CNT` Transaction Content

`CNT` is the most important repeated subfield field in the base transaction model.[^nist-draft]

OpenNist interprets it as:

- subfield `1`: transaction-level summary entry
- subfields `2..n`: one logical record descriptor per record listed in the transaction

For the record descriptors, OpenNist currently relies on the first item in each subfield to determine the expected logical record type when decoding mixed fielded and binary transactions.

Practical meaning:

- the first item of each descriptor subfield is the record type
- later items typically include the IDC and may include profile-defined record metadata

This is why `CNT` matters for decoding opaque binary records: it tells the decoder which record type to expect next.

### `9.010 MIN` Minutiae Data

`MIN` is treated as repeated minutiae item groups.[^nist-draft]

OpenNist currently documents it at the field level as:

- one subfield per minutia entry
- one or more items inside that subfield

The precise item meaning depends on the minutiae format declared by `9.003 FMT` and on the transaction profile in use.

Because of that, OpenNist currently preserves and displays the parsed item groups generically instead of assigning fixed item names for every possible minutiae schema.

### Finger and position lists

Fields such as `FGP` may encode one or more position values.

OpenNist preserves those values exactly and splits them using the normal field parsing rules:

- multiple values inside one subfield are treated as multiple items
- repeated grouped values across subfields are treated as repeated subfields

For binary-derived fingerprint image headers, OpenNist exposes `FGP` as a derived display value rather than as a separately named item schema.

## What the browser inspector shows

The NIST explorer in the browser:

- shows a field row for every parsed field
- shows nested subfield rows when a field has more than one subfield or more than one item
- shows nested item rows under a subfield when that subfield contains multiple item values

This means:

- plain scalar fields stay compact
- grouped fields such as `CNT` and `MIN` expand into a deeper tree
- OpenNist does not discard subfield and item structure during parsing

## Item naming model

OpenNist parses subfields generically across fielded NIST-family records.

The docs and app provide:

- complete generic parsing behavior
- field-level labels for the built-in catalog
- specific guidance for the most important repeated fields such as `CNT` and `MIN`

When a field is defined positionally by a profile or interchange format, OpenNist keeps the parsed item order intact and presents those items by position rather than inventing item names.

[^nist-draft]: [NIST SP 500-290e4, ANSI/NIST-ITL 1-2025 Balloted Draft](https://www.nist.gov/document/ansi-nist-itl-1-2025-balloted-draft)
[^nist-program]: [ANSI/NIST-ITL Standard Working Groups](https://www.nist.gov/itl/iad/image-group/ansinist-itl-standard-working-groups)
