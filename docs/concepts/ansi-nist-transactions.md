# Concept: ANSI/NIST Transactions and Logical Records

ANSI/NIST transaction files are structured as a sequence of logical records. Each record has a type, and fielded records contain tagged fields such as `1.001` or `14.999`.

## Core ideas

### Logical records

A transaction is not one flat table. It is a sequence of records, each with its own purpose.

Examples:

- Type-1 often carries transaction-level metadata
- image-bearing records use other record types
- some records are fielded text records
- some are opaque binary records

### Fields, subfields, and items

In fielded records, each field has a tag:

- record type
- field number

Some fields contain:

- one value
- multiple subfields
- multiple items inside a subfield

That matters because a field may look simple in a text dump but actually carry structured repeated data.

### Binary records

Not every record is naturally represented as text. Some record types carry opaque binary payloads or fixed-layout binary headers. OpenNist preserves those records so they can be round-tripped without losing bytes.

## Filename extensions

The same transaction family may appear under different extensions:

- `.an2`
- `.nist`
- `.eft`

The extension is convention. The record structure is what matters.

## How OpenNist models this

`OpenNist.Nist` decodes the transaction into:

- `NistFile`
- `NistRecord`
- `NistField`
- `NistTag`

Fielded records are parsed into tags and values. Opaque binary records are preserved as encoded bytes so they can be re-emitted exactly.
