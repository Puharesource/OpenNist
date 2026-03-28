# Reference: NIST Record and Field Reference

This page documents the built-in ANSI/NIST record and field catalog shipped in OpenNist.

It is aligned with the metadata used by the browser NIST explorer, so the labels in the docs match the labels you see in the app.

## Scope

This reference covers:

- every record type currently labeled by OpenNist
- every built-in record-specific field label currently exposed by OpenNist
- the generic `LEN` field that applies across fielded records
- derived binary-header fields and derived `DATA` payload fields that OpenNist exposes for opaque binary image records

## Generic field

| Tag | Mnemonic | Name | Value type | Notes |
| --- | --- | --- | --- | --- |
| `x.001` | `LEN` | Logical record length | Unsigned integer byte count | Byte length of the logical record as stored in the file. |

## Type-1: Transaction Information

Top-level transaction header with version, content summary, and routing metadata.

| Tag | Mnemonic | Name | Value type | Notes |
| --- | --- | --- | --- | --- |
| `1.001` | `LEN` | Logical record length | Unsigned integer byte count | Byte length of the logical record. |
| `1.002` | `VER` | Version | Version code string | ANSI/NIST transaction version declared by the file. |
| `1.003` | `CNT` | Transaction content | Repeated `[record type, IDC]` pairs | Lists the logical record types and IDC values present in the transaction. |
| `1.004` | `TOT` | Type of transaction | Transaction code | High-level transaction purpose or workflow code. |
| `1.005` | `DAT` | Date | Calendar date in `YYYYMMDD` | Transaction date when present. |
| `1.006` | `PRY` | Priority | Priority code | Processing priority requested for the transaction. |
| `1.007` | `DAI` | Destination agency identifier | Agency identifier text | Receiving agency or system identifier. |
| `1.008` | `ORI` | Originating agency identifier | Agency identifier text | Submitting agency or system identifier. |
| `1.009` | `TCN` | Transaction control number | Identifier text | Unique transaction identifier used for correlation and tracking. |
| `1.010` | `TCR` | Transaction control reference | Identifier text | Reference to a related or prior transaction control number. |
| `1.011` | `NSR` | Native scanning resolution | Resolution value | Native image scanning resolution declared for the transaction. |
| `1.012` | `NTR` | Nominal transmitting resolution | Resolution value | Nominal resolution used for interchange. |
| `1.013` | `DOM` | Domain name | Domain identifier text | Domain or profile identifier that qualifies transaction-specific semantics. |
| `1.014` | `DTS` | Domain-specific timestamp | Timestamp text | UTC timestamp when present in Type-1 headers. |
| `1.015` | `DCS` | Character encoding | Repeated encoding declarations | Character set declarations used by subsequent textual fields. |
| `1.027` |  | Profile-defined field 027 | Profile-defined text or subfields | Type-1 extension field used by the selected transaction profile or vendor implementation. |

## Type-2: User-Defined Descriptive Text

Free-form descriptive text attached to the transaction.

| Tag | Mnemonic | Name | Value type | Notes |
| --- | --- | --- | --- | --- |
| `2.001` | `LEN` | Logical record length | Unsigned integer byte count | Byte length of the logical record. |
| `2.002` | `IDC` | Image designation character | Small unsigned integer | Logical image slot or item reference within the transaction. |
| `2.003` | `TXT` | Descriptive text | Free-form text | User-defined or profile-defined descriptive text. |
| `2.030` |  | Profile-defined field 030 | Profile-defined text or subfields | Type-2 extension field used by the selected transaction profile or vendor implementation. |
| `2.031` |  | Profile-defined field 031 | Profile-defined text or subfields | Type-2 extension field used by the selected transaction profile or vendor implementation. |
| `2.032` |  | Profile-defined field 032 | Profile-defined text or subfields | Type-2 extension field used by the selected transaction profile or vendor implementation. |

## Type-3: Low-Resolution Grayscale Fingerprint Image

Traditional binary grayscale fingerprint image record.

OpenNist exposes the fixed binary header as derived fields in the app, plus a derived `DATA` payload field.

| Tag | Mnemonic | Name | Value type | Notes |
| --- | --- | --- | --- | --- |
| `3.001` | `LEN` | Logical record length | Unsigned integer byte count | Byte length of the logical record. |
| `3.002` | `IDC` | Image designation character | Small unsigned integer | Logical image slot or item reference within the transaction. |
| `3.003` | `IMP` | Impression type | Impression code | Encoded impression or capture type. |
| `3.004` | `FGP` | Finger position | Finger position code(s) | One or more finger position codes. |
| `3.005` | `ISR` | Image scanning resolution | Resolution code | Stored scanning resolution code. |
| `3.006` | `HLL` | Horizontal line length | Pixel width | Pixel width decoded from the fixed binary header. |
| `3.007` | `VLL` | Vertical line length | Pixel height | Pixel height decoded from the fixed binary header. |
| `3.008` | `GCA` | Grayscale compression algorithm | Compression code | Compression indicator decoded from the fixed binary header. |
| `3.999` | `DATA` | Image data | Binary image payload | Derived binary low-resolution grayscale fingerprint image payload preserved from the opaque record body. |

## Type-4: High-Resolution Grayscale Fingerprint Image

Traditional binary 8-bit grayscale fingerprint image record.

OpenNist exposes the fixed binary header as derived fields in the app, plus a derived `DATA` payload field.

| Tag | Mnemonic | Name | Value type | Notes |
| --- | --- | --- | --- | --- |
| `4.001` | `LEN` | Logical record length | Unsigned integer byte count | Byte length of the logical record. |
| `4.002` | `IDC` | Image designation character | Small unsigned integer | Logical image slot or item reference within the transaction. |
| `4.003` | `IMP` | Impression type | Impression code | Encoded impression or capture type. |
| `4.004` | `FGP` | Finger position | Finger position code(s) | One or more finger position codes. |
| `4.005` | `ISR` | Image scanning resolution | Resolution code | Stored scanning resolution code. |
| `4.006` | `HLL` | Horizontal line length | Pixel width | Pixel width decoded from the fixed binary header. |
| `4.007` | `VLL` | Vertical line length | Pixel height | Pixel height decoded from the fixed binary header. |
| `4.008` | `GCA` | Grayscale compression algorithm | Compression code | Compression indicator decoded from the fixed binary header. |
| `4.999` | `DATA` | Image data | Binary image payload | Derived binary fingerprint image payload preserved from the opaque record body. |

## Type-5: Low-Resolution Binary Fingerprint Image

Traditional binary black-and-white fingerprint image record.

OpenNist exposes the fixed binary header as derived fields in the app, plus a derived `DATA` payload field.

| Tag | Mnemonic | Name | Value type | Notes |
| --- | --- | --- | --- | --- |
| `5.001` | `LEN` | Logical record length | Unsigned integer byte count | Byte length of the logical record. |
| `5.002` | `IDC` | Image designation character | Small unsigned integer | Logical image slot or item reference within the transaction. |
| `5.003` | `IMP` | Impression type | Impression code | Encoded impression or capture type. |
| `5.004` | `FGP` | Finger position | Finger position code(s) | One or more finger position codes. |
| `5.005` | `ISR` | Image scanning resolution | Resolution code | Stored scanning resolution code. |
| `5.006` | `HLL` | Horizontal line length | Pixel width | Pixel width decoded from the fixed binary header. |
| `5.007` | `VLL` | Vertical line length | Pixel height | Pixel height decoded from the fixed binary header. |
| `5.008` | `BCA` | Binary compression algorithm | Compression code | Compression indicator decoded from the fixed binary header. |
| `5.999` | `DATA` | Image data | Binary image payload | Derived binary low-resolution bitonal fingerprint image payload preserved from the opaque record body. |

## Type-6: High-Resolution Binary Fingerprint Image

Traditional binary black-and-white high-resolution fingerprint image record.

OpenNist exposes the fixed binary header as derived fields in the app, plus a derived `DATA` payload field.

| Tag | Mnemonic | Name | Value type | Notes |
| --- | --- | --- | --- | --- |
| `6.001` | `LEN` | Logical record length | Unsigned integer byte count | Byte length of the logical record. |
| `6.002` | `IDC` | Image designation character | Small unsigned integer | Logical image slot or item reference within the transaction. |
| `6.003` | `IMP` | Impression type | Impression code | Encoded impression or capture type. |
| `6.004` | `FGP` | Finger position | Finger position code(s) | One or more finger position codes. |
| `6.005` | `ISR` | Image scanning resolution | Resolution code | Stored scanning resolution code. |
| `6.006` | `HLL` | Horizontal line length | Pixel width | Pixel width decoded from the fixed binary header. |
| `6.007` | `VLL` | Vertical line length | Pixel height | Pixel height decoded from the fixed binary header. |
| `6.008` | `BCA` | Binary compression algorithm | Compression code | Compression indicator decoded from the fixed binary header. |
| `6.999` | `DATA` | Image data | Binary image payload | Derived binary high-resolution bitonal fingerprint image payload preserved from the opaque record body. |

## Type-7: User-Defined Image

Traditional binary user-defined image record.

OpenNist exposes the fixed binary header as derived fields in the app, plus a derived `DATA` payload field.

| Tag | Mnemonic | Name | Value type | Notes |
| --- | --- | --- | --- | --- |
| `7.001` | `LEN` | Logical record length | Unsigned integer byte count | Byte length of the logical record. |
| `7.002` | `IDC` | Image designation character | Small unsigned integer | Logical image slot or item reference within the transaction. |
| `7.003` | `IMP` | Impression type | Impression code | Encoded impression or capture type. |
| `7.004` | `FGP` | Finger position | Finger position code(s) | One or more finger position codes when present in the header layout. |
| `7.005` | `ISR` | Image scanning resolution | Resolution code | Stored scanning resolution code. |
| `7.006` | `HLL` | Horizontal line length | Pixel width | Pixel width decoded from the fixed binary header. |
| `7.007` | `VLL` | Vertical line length | Pixel height | Pixel height decoded from the fixed binary header. |
| `7.008` | `CGA` | Compression algorithm | Compression code | Compression indicator decoded from the fixed binary header. |
| `7.999` | `DATA` | Image data | Binary image payload | Derived binary user-defined image payload preserved from the opaque record body. |

## Type-8: Signature Image

Signature or signature-like image record.

OpenNist exposes the fixed binary header as derived fields in the app, plus a derived `DATA` payload field.

| Tag | Mnemonic | Name | Value type | Notes |
| --- | --- | --- | --- | --- |
| `8.001` | `LEN` | Logical record length | Unsigned integer byte count | Byte length of the logical record. |
| `8.002` | `IDC` | Image designation character | Small unsigned integer | Logical image slot or item reference within the transaction. |
| `8.003` | `SIG` | Signature type | Signature classification code | Signature classification code from the fixed binary header. |
| `8.004` | `SRT` | Signature representation | Representation code | Signature representation code from the fixed binary header. |
| `8.005` | `ISR` | Image scanning resolution | Resolution code | Stored scanning resolution code. |
| `8.006` | `HLL` | Horizontal line length | Pixel width | Pixel width decoded from the fixed binary header. |
| `8.007` | `VLL` | Vertical line length | Pixel height | Pixel height decoded from the fixed binary header. |
| `8.999` | `DATA` | Image data | Binary image payload | Binary signature payload. |

## Type-9: Minutiae Data

Feature points and related fingerprint comparison data.

| Tag | Mnemonic | Name | Value type | Notes |
| --- | --- | --- | --- | --- |
| `9.001` | `LEN` | Logical record length | Unsigned integer byte count | Byte length of the logical record. |
| `9.002` | `IDC` | Image designation character | Small unsigned integer | Logical image slot or item reference within the transaction. |
| `9.003` | `FMT` | Minutiae format | Format code | Identifies the minutiae layout or interchange format. |
| `9.004` | `OFR` | Origin | Origin or source code | Indicates the source or origin of the minutiae data. |
| `9.005` | `FPC` | Fingerprint pattern classification | Pattern code | Pattern classification value. |
| `9.006` |  | Legacy minutiae field 006 | Legacy format-specific text or subfields | Legacy Type-9 minutiae exchange field. The exact layout depends on the minutiae format and transaction profile. |
| `9.007` |  | Legacy minutiae field 007 | Legacy format-specific text or subfields | Legacy Type-9 minutiae exchange field. The exact layout depends on the minutiae format and transaction profile. |
| `9.008` |  | Legacy minutiae field 008 | Legacy format-specific text or subfields | Legacy Type-9 minutiae exchange field. The exact layout depends on the minutiae format and transaction profile. |
| `9.009` |  | Legacy minutiae field 009 | Legacy format-specific text or subfields | Legacy Type-9 minutiae exchange field. The exact layout depends on the minutiae format and transaction profile. |
| `9.010` | `MIN` | Minutiae data | Repeated minutiae item groups | Repeating minutiae entries. Subfield and item layout depends on the selected minutiae format. |
| `9.011` |  | Legacy minutiae field 011 | Legacy format-specific text or subfields | Legacy Type-9 minutiae exchange field. The exact layout depends on the minutiae format and transaction profile. |
| `9.012` |  | Legacy minutiae field 012 | Legacy format-specific text or subfields | Legacy Type-9 minutiae exchange field. The exact layout depends on the minutiae format and transaction profile. |
| `9.014` |  | Legacy minutiae field 014 | Legacy format-specific text or subfields | Legacy Type-9 minutiae exchange field. The exact layout depends on the minutiae format and transaction profile. |
| `9.015` |  | Legacy minutiae field 015 | Legacy format-specific text or subfields | Legacy Type-9 minutiae exchange field. The exact layout depends on the minutiae format and transaction profile. |
| `9.016` |  | Legacy minutiae field 016 | Legacy format-specific text or subfields | Legacy Type-9 minutiae exchange field. The exact layout depends on the minutiae format and transaction profile. |
| `9.017` |  | Legacy minutiae field 017 | Legacy format-specific text or subfields | Legacy Type-9 minutiae exchange field. The exact layout depends on the minutiae format and transaction profile. |
| `9.020` |  | Legacy minutiae field 020 | Legacy format-specific text or subfields | Legacy Type-9 minutiae exchange field. The exact layout depends on the minutiae format and transaction profile. |
| `9.021` |  | Legacy minutiae field 021 | Legacy format-specific text or subfields | Legacy Type-9 minutiae exchange field. The exact layout depends on the minutiae format and transaction profile. |
| `9.022` |  | Legacy minutiae field 022 | Legacy format-specific text or subfields | Legacy Type-9 minutiae exchange field. The exact layout depends on the minutiae format and transaction profile. |
| `9.023` |  | Legacy minutiae field 023 | Legacy format-specific text or subfields | Legacy Type-9 minutiae exchange field. The exact layout depends on the minutiae format and transaction profile. |
| `9.126` |  | Extended minutiae field 126 | Format-specific text or subfields | Extended Type-9 minutiae field used by format-specific or legacy interchange profiles. |
| `9.127` |  | Extended minutiae field 127 | Format-specific text or subfields | Extended Type-9 minutiae field used by format-specific or legacy interchange profiles. |
| `9.128` |  | Extended minutiae field 128 | Format-specific text or subfields | Extended Type-9 minutiae field used by format-specific or legacy interchange profiles. |
| `9.129` |  | Extended minutiae field 129 | Format-specific text or subfields | Extended Type-9 minutiae field used by format-specific or legacy interchange profiles. |
| `9.130` |  | Extended minutiae field 130 | Format-specific text or subfields | Extended Type-9 minutiae field used by format-specific or legacy interchange profiles. |
| `9.131` |  | Extended minutiae field 131 | Format-specific text or subfields | Extended Type-9 minutiae field used by format-specific or legacy interchange profiles. |
| `9.132` |  | Extended minutiae field 132 | Format-specific text or subfields | Extended Type-9 minutiae field used by format-specific or legacy interchange profiles. |
| `9.133` |  | Extended minutiae field 133 | Format-specific text or subfields | Extended Type-9 minutiae field used by format-specific or legacy interchange profiles. |
| `9.134` |  | Extended minutiae field 134 | Format-specific text or subfields | Extended Type-9 minutiae field used by format-specific or legacy interchange profiles. |
| `9.135` |  | Extended minutiae field 135 | Format-specific text or subfields | Extended Type-9 minutiae field used by format-specific or legacy interchange profiles. |
| `9.136` |  | Extended minutiae field 136 | Format-specific text or subfields | Extended Type-9 minutiae field used by format-specific or legacy interchange profiles. |
| `9.137` |  | Extended minutiae field 137 | Format-specific text or subfields | Extended Type-9 minutiae field used by format-specific or legacy interchange profiles. |
| `9.138` |  | Extended minutiae field 138 | Format-specific text or subfields | Extended Type-9 minutiae field used by format-specific or legacy interchange profiles. |
| `9.139` |  | Extended minutiae field 139 | Format-specific text or subfields | Extended Type-9 minutiae field used by format-specific or legacy interchange profiles. |
| `9.140` |  | Extended minutiae field 140 | Format-specific text or subfields | Extended Type-9 minutiae field used by format-specific or legacy interchange profiles. |

## Type-10: Facial, Scar, Mark, and Tattoo Image

Fielded image record for face and SMT imagery.

| Tag | Mnemonic | Name | Value type | Notes |
| --- | --- | --- | --- | --- |
| `10.001` | `LEN` | Logical record length | Unsigned integer byte count | Byte length of the logical record. |
| `10.002` | `IDC` | Image designation character | Small unsigned integer | Logical image slot or item reference within the transaction. |
| `10.003` | `IMT` | Image type | Image category code | Image category such as face, scar, mark, or tattoo. |
| `10.004` | `SRC` | Source agency | Agency identifier text | Source agency or submitting system identifier. |
| `10.005` | `PHD` | Capture date | Calendar date in `YYYYMMDD` | Capture date when present. |
| `10.006` | `HLL` | Horizontal line length | Pixel width | Pixel width. |
| `10.007` | `VLL` | Vertical line length | Pixel height | Pixel height. |
| `10.008` | `SLC` | Scale units | Scale unit code | Unit system used by the transmitted pixel scale fields. |
| `10.009` | `THPS` | Transmitted horizontal pixel scale | Pixels per unit | Horizontal transmitted pixel density or sampling scale. |
| `10.010` | `TVPS` | Transmitted vertical pixel scale | Pixels per unit | Vertical transmitted pixel density or sampling scale. |
| `10.011` | `CGA` | Compression algorithm | Compression code | Compression code for the image payload. |
| `10.012` | `CSP` | Color space | Color space code | Color space or channel interpretation used by the image payload. |
| `10.013` | `SAP` | Subject acquisition profile | Profile code | Acquisition profile level describing how the subject image was captured. |
| `10.020` | `POS` | Subject pose | Pose code | Primary subject pose classification for a face image. |
| `10.021` | `POA` | Pose offset angle | Angle or pose offset value | Pose offset angle recorded for the subject pose. |
| `10.023` | `PAS` | Photo acquisition source | Acquisition source code | Source or scenario used to acquire the photo. |
| `10.026` | `SXS` | Subject facial description | Description code | Visible facial expression or subject presentation description. |
| `10.027` | `SEC` | Subject eye color | Eye color code | Observed eye color. |
| `10.028` | `SHC` | Subject hair color | Hair color code | Observed hair color. |
| `10.040` | `SMT` | Scar, mark, or tattoo classification | Classification text or code | Classification code for the scar, mark, or tattoo shown in the image. |
| `10.042` | `SMD` | Scar, mark, or tattoo descriptors | Repeated descriptor subfields | Structured descriptors that further describe the scar, mark, or tattoo. |
| `10.999` | `DATA` | Image data | Binary image payload | Binary face, scar, mark, or tattoo image payload. |

## Type-13: Latent Fingerprint Image

Fielded image record for latent fingerprint imagery.

| Tag | Mnemonic | Name | Value type | Notes |
| --- | --- | --- | --- | --- |
| `13.001` | `LEN` | Logical record length | Unsigned integer byte count | Byte length of the logical record. |
| `13.002` | `IDC` | Image designation character | Small unsigned integer | Logical image slot or item reference within the transaction. |
| `13.003` | `IMP` | Impression type | Impression code | Encoded impression or capture type. |
| `13.004` | `SRC` | Source agency | Agency identifier text | Source agency or submitting system identifier. |
| `13.005` | `LCD` | Capture date | Calendar date in `YYYYMMDD` | Latent capture date when present. |
| `13.006` | `HLL` | Horizontal line length | Pixel width | Pixel width. |
| `13.007` | `VLL` | Vertical line length | Pixel height | Pixel height. |
| `13.008` | `SLC` | Scale units | Scale unit code | Unit system used by the transmitted pixel scale fields. |
| `13.009` | `THPS` | Transmitted horizontal pixel scale | Pixels per unit | Horizontal transmitted pixel density or sampling scale. |
| `13.010` | `TVPS` | Transmitted vertical pixel scale | Pixels per unit | Vertical transmitted pixel density or sampling scale. |
| `13.011` | `CGA` | Compression algorithm | Compression code | Compression code for the image payload. |
| `13.012` | `BPX` | Bits per pixel | Integer bits-per-pixel value | Bit depth used by the image payload. |
| `13.013` | `FGP` | Friction ridge generalized position | Finger position code(s) | Generalized finger or friction ridge position associated with the latent image. |
| `13.014` | `SPD` | Search position descriptors | Repeated descriptor items | Descriptors used to qualify the latent search position. |
| `13.015` | `PPC` | Print position coordinates | Repeated coordinate values | Coordinates describing the position of the latent print within a larger image or lift. |
| `13.999` | `DATA` | Image data | Binary image payload | Binary latent fingerprint image payload. |

## Type-14: Fingerprint Image

Fielded variable-resolution fingerprint image record.

| Tag | Mnemonic | Name | Value type | Notes |
| --- | --- | --- | --- | --- |
| `14.001` | `LEN` | Logical record length | Unsigned integer byte count | Byte length of the logical record. |
| `14.002` | `IDC` | Image designation character | Small unsigned integer | Logical image slot or item reference within the transaction. |
| `14.003` | `IMP` | Impression type | Impression code | Encoded impression or capture type. |
| `14.004` | `SRC` | Source agency | Agency identifier text | Source agency or submitting system identifier. |
| `14.005` | `FCD` | Capture date | Calendar date in `YYYYMMDD` | Fingerprint capture date when present. |
| `14.006` | `HLL` | Horizontal line length | Pixel width | Pixel width. |
| `14.007` | `VLL` | Vertical line length | Pixel height | Pixel height. |
| `14.008` | `SLC` | Scale units | Scale unit code | Unit system used by the transmitted pixel scale fields. |
| `14.009` | `THPS` | Transmitted horizontal pixel scale | Pixels per unit | Horizontal transmitted pixel density or sampling scale. |
| `14.010` | `TVPS` | Transmitted vertical pixel scale | Pixels per unit | Vertical transmitted pixel density or sampling scale. |
| `14.011` | `CGA` | Compression algorithm | Compression code | Compression code for the image payload. |
| `14.012` | `BPX` | Bits per pixel | Integer bits-per-pixel value | Bit depth used by the image payload. |
| `14.013` | `FGP` | Friction ridge generalized position | Finger position code(s) | Generalized finger position associated with the fingerprint image. |
| `14.014` | `PPD` | Print position descriptors | Repeated descriptor items | Descriptors such as tip, joint segment, or view qualifiers for the fingerprint image. |
| `14.015` | `PPC` | Print position coordinates | Repeated coordinate values | Coordinates describing the positioned print region. |
| `14.018` | `AMP` | Amputated or bandaged | Repeated condition codes | Indicators that the finger is amputated, bandaged, or otherwise unavailable. |
| `14.022` | `NQM` | Fingerprint quality metric | Legacy quality metric | Legacy fingerprint quality value retained for interchange with older profiles. |
| `14.999` | `DATA` | Image data | Binary image payload | Binary fingerprint image payload. |

## Type-15: Palmprint Image

Fielded variable-resolution palmprint image record.

| Tag | Mnemonic | Name | Value type | Notes |
| --- | --- | --- | --- | --- |
| `15.001` | `LEN` | Logical record length | Unsigned integer byte count | Byte length of the logical record. |
| `15.002` | `IDC` | Image designation character | Small unsigned integer | Logical image slot or item reference within the transaction. |
| `15.003` | `IMP` | Impression type | Impression code | Encoded impression or capture type. |
| `15.004` | `SRC` | Source agency | Agency identifier text | Source agency or submitting system identifier. |
| `15.005` | `PCD` | Capture date | Calendar date in `YYYYMMDD` | Palm capture date when present. |
| `15.006` | `HLL` | Horizontal line length | Pixel width | Pixel width. |
| `15.007` | `VLL` | Vertical line length | Pixel height | Pixel height. |
| `15.008` | `SLC` | Scale units | Scale unit code | Unit system used by the transmitted pixel scale fields. |
| `15.009` | `THPS` | Transmitted horizontal pixel scale | Pixels per unit | Horizontal transmitted pixel density or sampling scale. |
| `15.010` | `TVPS` | Transmitted vertical pixel scale | Pixels per unit | Vertical transmitted pixel density or sampling scale. |
| `15.011` | `CGA` | Compression algorithm | Compression code | Compression code for the image payload. |
| `15.012` | `BPX` | Bits per pixel | Integer bits-per-pixel value | Bit depth used by the image payload. |
| `15.013` | `FGP` | Friction ridge generalized position | Palm or friction ridge position code(s) | Generalized palm or friction ridge position associated with the image. |
| `15.999` | `DATA` | Image data | Binary image payload | Binary palmprint image payload. |

## Type-16: Test Image

Fielded record used for testing and diagnostics.

| Tag | Mnemonic | Name | Value type | Notes |
| --- | --- | --- | --- | --- |
| `16.001` | `LEN` | Logical record length | Unsigned integer byte count | Byte length of the logical record. |
| `16.002` | `IDC` | Image designation character | Small unsigned integer | Logical image slot or item reference within the transaction. |
| `16.003` | `UDI` | User-defined image type | User-defined type code | User-defined classification for the test image. |
| `16.004` | `SRC` | Source agency | Agency identifier text | Source agency or submitting system identifier. |
| `16.005` | `UTD` | Test capture date | Calendar date in `YYYYMMDD` | Capture date for the test image when present. |
| `16.006` | `HLL` | Horizontal line length | Pixel width | Pixel width. |
| `16.007` | `VLL` | Vertical line length | Pixel height | Pixel height. |
| `16.008` | `SLC` | Scale units | Scale unit code | Unit system used by the transmitted pixel scale fields. |
| `16.009` | `THPS` | Transmitted horizontal pixel scale | Pixels per unit | Horizontal transmitted pixel density or sampling scale. |
| `16.010` | `TVPS` | Transmitted vertical pixel scale | Pixels per unit | Vertical transmitted pixel density or sampling scale. |
| `16.011` | `CGA` | Compression algorithm | Compression code | Compression code for the image payload. |
| `16.012` | `BPX` | Bits per pixel | Integer bits-per-pixel value | Bit depth used by the image payload. |
| `16.013` | `CSP` | Color space | Color space code | Color space or channel interpretation used by the test image. |
| `16.999` | `DATA` | Image data | Binary image payload | Binary test image payload. |

## Type-17: Iris Image

Fielded iris image record.

| Tag | Mnemonic | Name | Value type | Notes |
| --- | --- | --- | --- | --- |
| `17.001` | `LEN` | Logical record length | Unsigned integer byte count | Byte length of the logical record. |
| `17.002` | `IDC` | Image designation character | Small unsigned integer | Logical image slot or item reference within the transaction. |
| `17.003` | `ELR` | Eye label | Eye label code | Identifies which eye is represented. |
| `17.004` | `SRC` | Source agency | Agency identifier text | Source agency or submitting system identifier. |
| `17.005` | `ICD` | Iris capture date | Calendar date in `YYYYMMDD` | Date that the iris image data was captured. |
| `17.006` | `HLL` | Horizontal line length | Pixel width | Pixel width. |
| `17.007` | `VLL` | Vertical line length | Pixel height | Pixel height. |
| `17.008` | `SLC` | Scale units | Scale unit code | Unit system used by the transmitted pixel scale fields. |
| `17.009` | `THPS` | Transmitted horizontal pixel scale | Pixels per unit | Horizontal transmitted pixel density or sampling scale. |
| `17.010` | `TVPS` | Transmitted vertical pixel scale | Pixels per unit | Vertical transmitted pixel density or sampling scale. |
| `17.011` | `CGA` | Compression algorithm | Compression code | Compression code for the image payload. |
| `17.012` | `BPX` | Bits per pixel | Integer bits-per-pixel value | Bit depth used by the image payload. |
| `17.013` | `CSP` | Color space | Color space code | Color space or channel interpretation used by the iris image payload. |
| `17.999` | `DATA` | Image data | Binary image payload | Binary iris image payload. |
