# Glossary

## ANSI/NIST

A family of biometric transaction formats built around logical records and tagged fields.

## AN2

A common filename extension used for ANSI/NIST-style transaction files.

## EFT

Electronic Fingerprint Transmission. In practice this often means an ANSI/NIST-family transaction shaped by a profile such as EBTS.

## Logical record

A top-level record inside a NIST-family transaction. Records may be fielded text records or opaque binary records.

## Field

A tagged value inside a fielded logical record, such as `1.001`.

## Subfield

A repeated or structured subdivision inside a field value.

## Item

A lower-level value inside a subfield.

## WSQ

Wavelet Scalar Quantization, a fingerprint-focused compression format for grayscale imagery.

## NFIQ 2

NIST Fingerprint Image Quality 2, a fingerprint quality scoring system that analyzes image characteristics and returns a quality score and supporting measures.

## PPI

Pixels per inch. Fingerprint workflows often care about acquisition resolution, and some algorithms require a specific input resolution.
