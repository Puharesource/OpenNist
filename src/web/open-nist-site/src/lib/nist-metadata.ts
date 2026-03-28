export type NistRecordMetadata = {
  name: string
  description: string
}

export type NistFieldMetadata = {
  label: string
  mnemonic?: string
  description?: string
  valueType?: string
  imagePayload?: boolean
}

function createField(
  label: string,
  mnemonic?: string,
  description?: string,
  valueType?: string,
  imagePayload?: boolean
): NistFieldMetadata {
  return { label, mnemonic, description, valueType, imagePayload }
}

function createProfileField(recordType: number, fieldNumber: number): NistFieldMetadata {
  return createField(
    `Profile-defined field ${fieldNumber.toString().padStart(3, "0")}`,
    undefined,
    `Type-${recordType} extension field carried by the selected transaction profile or vendor implementation.`,
    "Profile-defined text or subfields"
  )
}

function createLegacyMinutiaeField(fieldNumber: number): NistFieldMetadata {
  return createField(
    `Legacy minutiae field ${fieldNumber.toString().padStart(3, "0")}`,
    undefined,
    "Legacy Type-9 minutiae exchange field. The exact layout depends on the minutiae format and transaction profile.",
    "Legacy format-specific text or subfields"
  )
}

function createExtendedMinutiaeField(fieldNumber: number): NistFieldMetadata {
  return createField(
    `Extended minutiae field ${fieldNumber.toString().padStart(3, "0")}`,
    undefined,
    "Extended Type-9 minutiae field used by format-specific or legacy interchange profiles.",
    "Format-specific text or subfields"
  )
}

function createOpaqueImageDataField(description: string): NistFieldMetadata {
  return createField("Image data", "DATA", description, "Binary image payload", true)
}

const scaleUnitsField = createField(
  "Scale units",
  "SLC",
  "Unit system used by the transmitted pixel scale fields.",
  "Scale unit code"
)

const transmittedHorizontalPixelScaleField = createField(
  "Transmitted horizontal pixel scale",
  "THPS",
  "Horizontal transmitted pixel density or sampling scale for the image.",
  "Pixels per unit"
)

const transmittedVerticalPixelScaleField = createField(
  "Transmitted vertical pixel scale",
  "TVPS",
  "Vertical transmitted pixel density or sampling scale for the image.",
  "Pixels per unit"
)

const compressionAlgorithmField = createField(
  "Compression algorithm",
  "CGA",
  "Compression code for the stored image payload.",
  "Compression code"
)

const bitsPerPixelField = createField(
  "Bits per pixel",
  "BPX",
  "Bit depth used by the image payload.",
  "Integer bits-per-pixel value"
)

const sourceAgencyField = createField(
  "Source agency",
  "SRC",
  "Submitting agency or source system identifier.",
  "Agency identifier text"
)

const imageDesignationCharacterField = createField(
  "Image designation character",
  "IDC",
  "Logical image slot or item reference within the transaction.",
  "Small unsigned integer"
)

const horizontalLineLengthField = createField(
  "Horizontal line length",
  "HLL",
  "Pixel width of the image.",
  "Pixel width"
)

const verticalLineLengthField = createField("Vertical line length", "VLL", "Pixel height of the image.", "Pixel height")

const RECORD_METADATA: Record<number, NistRecordMetadata> = {
  1: {
    name: "Transaction information",
    description: "Top-level transaction header with version, content summary, and routing metadata."
  },
  2: {
    name: "User-defined descriptive text",
    description: "Free-form descriptive text attached to the transaction."
  },
  3: {
    name: "Low-resolution grayscale fingerprint image",
    description: "Traditional binary grayscale fingerprint image record."
  },
  4: {
    name: "High-resolution grayscale fingerprint image",
    description: "Traditional binary 8-bit grayscale fingerprint image record."
  },
  5: {
    name: "Low-resolution binary fingerprint image",
    description: "Traditional binary black-and-white fingerprint image record."
  },
  6: {
    name: "High-resolution binary fingerprint image",
    description: "Traditional binary black-and-white high-resolution fingerprint image record."
  },
  7: {
    name: "User-defined image",
    description: "Traditional binary user-defined image record."
  },
  8: {
    name: "Signature image",
    description: "Signature or signature-like image record."
  },
  9: {
    name: "Minutiae data",
    description: "Feature points and related fingerprint comparison data."
  },
  10: {
    name: "Facial, scar, mark, and tattoo image",
    description: "Fielded image record for face and SMT imagery."
  },
  13: {
    name: "Latent fingerprint image",
    description: "Fielded image record for latent fingerprint imagery."
  },
  14: {
    name: "Fingerprint image",
    description: "Fielded variable-resolution fingerprint image record."
  },
  15: {
    name: "Palmprint image",
    description: "Fielded variable-resolution palmprint image record."
  },
  16: {
    name: "Test image",
    description: "Fielded record used for testing and diagnostics."
  },
  17: {
    name: "Iris image",
    description: "Fielded iris image record."
  }
}

const GENERIC_FIELD_METADATA: Record<number, NistFieldMetadata> = {
  1: createField(
    "Logical record length",
    "LEN",
    "Byte length of the logical record as stored in the file.",
    "Unsigned integer byte count"
  )
}

const FIELD_METADATA: Record<number, Record<number, NistFieldMetadata>> = {
  1: {
    2: createField("Version", "VER", "ANSI/NIST transaction version declared by the file.", "Version code string"),
    3: createField(
      "Transaction content",
      "CNT",
      "List of logical record types and IDC values present in the transaction.",
      "Repeated [record type, IDC] pairs"
    ),
    4: createField(
      "Type of transaction",
      "TOT",
      "High-level transaction purpose or workflow code.",
      "Transaction code"
    ),
    5: createField("Date", "DAT", "Transaction date when present.", "Calendar date in YYYYMMDD"),
    6: createField("Priority", "PRY", "Processing priority requested for the transaction.", "Priority code"),
    7: createField(
      "Destination agency identifier",
      "DAI",
      "Receiving agency or system identifier.",
      "Agency identifier text"
    ),
    8: createField(
      "Originating agency identifier",
      "ORI",
      "Submitting agency or system identifier.",
      "Agency identifier text"
    ),
    9: createField(
      "Transaction control number",
      "TCN",
      "Unique transaction identifier used for correlation and tracking.",
      "Identifier text"
    ),
    10: createField(
      "Transaction control reference",
      "TCR",
      "Reference to a related or prior transaction control number.",
      "Identifier text"
    ),
    11: createField(
      "Native scanning resolution",
      "NSR",
      "Native image scanning resolution declared for the transaction.",
      "Resolution value"
    ),
    12: createField(
      "Nominal transmitting resolution",
      "NTR",
      "Nominal resolution used for interchange.",
      "Resolution value"
    ),
    13: createField(
      "Domain name",
      "DOM",
      "Domain or profile identifier that qualifies transaction-specific semantics.",
      "Domain identifier text"
    ),
    14: createField(
      "Domain-specific timestamp",
      "DTS",
      "UTC timestamp when present in Type-1 headers.",
      "Timestamp text"
    ),
    15: createField(
      "Character encoding",
      "DCS",
      "Character set declarations used by subsequent textual fields.",
      "Repeated encoding declarations"
    ),
    27: createProfileField(1, 27)
  },
  2: {
    2: imageDesignationCharacterField,
    3: createField("Descriptive text", "TXT", "User-defined or profile-defined descriptive text.", "Free-form text"),
    30: createProfileField(2, 30),
    31: createProfileField(2, 31),
    32: createProfileField(2, 32)
  },
  3: {
    2: imageDesignationCharacterField,
    3: createField("Impression type", "IMP", "Encoded impression or capture type.", "Impression code"),
    4: createField("Finger position", "FGP", "One or more finger position codes.", "Finger position code(s)"),
    5: createField("Image scanning resolution", "ISR", "Stored scanning resolution code.", "Resolution code"),
    6: horizontalLineLengthField,
    7: verticalLineLengthField,
    8: createField(
      "Grayscale compression algorithm",
      "GCA",
      "Compression indicator decoded from the fixed binary header.",
      "Compression code"
    ),
    999: createOpaqueImageDataField(
      "Binary low-resolution grayscale fingerprint image payload preserved from the opaque record body."
    )
  },
  4: {
    2: imageDesignationCharacterField,
    3: createField("Impression type", "IMP", "Encoded impression or capture type.", "Impression code"),
    4: createField("Finger position", "FGP", "One or more finger position codes.", "Finger position code(s)"),
    5: createField("Image scanning resolution", "ISR", "Stored scanning resolution code.", "Resolution code"),
    6: horizontalLineLengthField,
    7: verticalLineLengthField,
    8: createField(
      "Grayscale compression algorithm",
      "GCA",
      "Compression indicator decoded from the fixed binary header.",
      "Compression code"
    ),
    999: createOpaqueImageDataField("Binary fingerprint image payload preserved from the opaque record body.")
  },
  5: {
    2: imageDesignationCharacterField,
    3: createField("Impression type", "IMP", "Encoded impression or capture type.", "Impression code"),
    4: createField("Finger position", "FGP", "One or more finger position codes.", "Finger position code(s)"),
    5: createField("Image scanning resolution", "ISR", "Stored scanning resolution code.", "Resolution code"),
    6: horizontalLineLengthField,
    7: verticalLineLengthField,
    8: createField(
      "Binary compression algorithm",
      "BCA",
      "Compression indicator decoded from the fixed binary header.",
      "Compression code"
    ),
    999: createOpaqueImageDataField(
      "Binary low-resolution bitonal fingerprint image payload preserved from the opaque record body."
    )
  },
  6: {
    2: imageDesignationCharacterField,
    3: createField("Impression type", "IMP", "Encoded impression or capture type.", "Impression code"),
    4: createField("Finger position", "FGP", "One or more finger position codes.", "Finger position code(s)"),
    5: createField("Image scanning resolution", "ISR", "Stored scanning resolution code.", "Resolution code"),
    6: horizontalLineLengthField,
    7: verticalLineLengthField,
    8: createField(
      "Binary compression algorithm",
      "BCA",
      "Compression indicator decoded from the fixed binary header.",
      "Compression code"
    ),
    999: createOpaqueImageDataField(
      "Binary high-resolution bitonal fingerprint image payload preserved from the opaque record body."
    )
  },
  7: {
    2: imageDesignationCharacterField,
    3: createField("Impression type", "IMP", "Encoded impression or capture type.", "Impression code"),
    4: createField(
      "Finger position",
      "FGP",
      "One or more finger position codes when present in the header layout.",
      "Finger position code(s)"
    ),
    5: createField("Image scanning resolution", "ISR", "Stored scanning resolution code.", "Resolution code"),
    6: horizontalLineLengthField,
    7: verticalLineLengthField,
    8: createField(
      "Compression algorithm",
      "CGA",
      "Compression indicator decoded from the fixed binary header.",
      "Compression code"
    ),
    999: createOpaqueImageDataField("Binary user-defined image payload preserved from the opaque record body.")
  },
  8: {
    2: imageDesignationCharacterField,
    3: createField(
      "Signature type",
      "SIG",
      "Signature classification code from the fixed binary header.",
      "Signature classification code"
    ),
    4: createField(
      "Signature representation",
      "SRT",
      "Signature representation code from the fixed binary header.",
      "Representation code"
    ),
    5: createField("Image scanning resolution", "ISR", "Stored scanning resolution code.", "Resolution code"),
    6: horizontalLineLengthField,
    7: verticalLineLengthField,
    999: createOpaqueImageDataField("Binary signature payload.")
  },
  9: {
    2: imageDesignationCharacterField,
    3: createField("Minutiae format", "FMT", "Identifies the minutiae layout or interchange format.", "Format code"),
    4: createField("Origin", "OFR", "Indicates the source or origin of the minutiae data.", "Origin or source code"),
    5: createField("Fingerprint pattern classification", "FPC", "Pattern classification value.", "Pattern code"),
    6: createLegacyMinutiaeField(6),
    7: createLegacyMinutiaeField(7),
    8: createLegacyMinutiaeField(8),
    9: createLegacyMinutiaeField(9),
    10: createField(
      "Minutiae data",
      "MIN",
      "Repeating minutiae entries. Subfield and item layout depends on the selected minutiae format.",
      "Repeated minutiae item groups"
    ),
    11: createLegacyMinutiaeField(11),
    12: createLegacyMinutiaeField(12),
    14: createLegacyMinutiaeField(14),
    15: createLegacyMinutiaeField(15),
    16: createLegacyMinutiaeField(16),
    17: createLegacyMinutiaeField(17),
    20: createLegacyMinutiaeField(20),
    21: createLegacyMinutiaeField(21),
    22: createLegacyMinutiaeField(22),
    23: createLegacyMinutiaeField(23),
    126: createExtendedMinutiaeField(126),
    127: createExtendedMinutiaeField(127),
    128: createExtendedMinutiaeField(128),
    129: createExtendedMinutiaeField(129),
    130: createExtendedMinutiaeField(130),
    131: createExtendedMinutiaeField(131),
    132: createExtendedMinutiaeField(132),
    133: createExtendedMinutiaeField(133),
    134: createExtendedMinutiaeField(134),
    135: createExtendedMinutiaeField(135),
    136: createExtendedMinutiaeField(136),
    137: createExtendedMinutiaeField(137),
    138: createExtendedMinutiaeField(138),
    139: createExtendedMinutiaeField(139),
    140: createExtendedMinutiaeField(140)
  },
  10: {
    2: imageDesignationCharacterField,
    3: createField("Image type", "IMT", "Image category such as face, scar, mark, or tattoo.", "Image category code"),
    4: sourceAgencyField,
    5: createField("Capture date", "PHD", "Capture date when present.", "Calendar date in YYYYMMDD"),
    6: horizontalLineLengthField,
    7: verticalLineLengthField,
    8: scaleUnitsField,
    9: transmittedHorizontalPixelScaleField,
    10: transmittedVerticalPixelScaleField,
    11: compressionAlgorithmField,
    12: createField(
      "Color space",
      "CSP",
      "Color space or channel interpretation used by the image payload.",
      "Color space code"
    ),
    13: createField(
      "Subject acquisition profile",
      "SAP",
      "Acquisition profile level describing how the subject image was captured.",
      "Profile code"
    ),
    20: createField("Subject pose", "POS", "Primary subject pose classification for a face image.", "Pose code"),
    21: createField(
      "Pose offset angle",
      "POA",
      "Pose offset angle recorded for the subject pose.",
      "Angle or pose offset value"
    ),
    23: createField(
      "Photo acquisition source",
      "PAS",
      "Source or scenario used to acquire the photo.",
      "Acquisition source code"
    ),
    26: createField(
      "Subject facial description",
      "SXS",
      "Visible facial expression or subject presentation description.",
      "Description code"
    ),
    27: createField("Subject eye color", "SEC", "Observed eye color.", "Eye color code"),
    28: createField("Subject hair color", "SHC", "Observed hair color.", "Hair color code"),
    40: createField(
      "Scar, mark, or tattoo classification",
      "SMT",
      "Classification code for the scar, mark, or tattoo shown in the image.",
      "Classification text or code"
    ),
    42: createField(
      "Scar, mark, or tattoo descriptors",
      "SMD",
      "Structured descriptors that further describe the scar, mark, or tattoo.",
      "Repeated descriptor subfields"
    ),
    999: createOpaqueImageDataField("Binary face, scar, mark, or tattoo image payload.")
  },
  13: {
    2: imageDesignationCharacterField,
    3: createField("Impression type", "IMP", "Encoded impression or capture type.", "Impression code"),
    4: sourceAgencyField,
    5: createField("Capture date", "LCD", "Latent capture date when present.", "Calendar date in YYYYMMDD"),
    6: horizontalLineLengthField,
    7: verticalLineLengthField,
    8: scaleUnitsField,
    9: transmittedHorizontalPixelScaleField,
    10: transmittedVerticalPixelScaleField,
    11: compressionAlgorithmField,
    12: bitsPerPixelField,
    13: createField(
      "Friction ridge generalized position",
      "FGP",
      "Generalized finger or friction ridge position associated with the latent image.",
      "Finger position code(s)"
    ),
    14: createField(
      "Search position descriptors",
      "SPD",
      "Descriptors used to qualify the latent search position.",
      "Repeated descriptor items"
    ),
    15: createField(
      "Print position coordinates",
      "PPC",
      "Coordinates describing the position of the latent print within a larger image or lift.",
      "Repeated coordinate values"
    ),
    999: createOpaqueImageDataField("Binary latent fingerprint image payload.")
  },
  14: {
    2: imageDesignationCharacterField,
    3: createField("Impression type", "IMP", "Encoded impression or capture type.", "Impression code"),
    4: sourceAgencyField,
    5: createField("Capture date", "FCD", "Fingerprint capture date when present.", "Calendar date in YYYYMMDD"),
    6: horizontalLineLengthField,
    7: verticalLineLengthField,
    8: scaleUnitsField,
    9: transmittedHorizontalPixelScaleField,
    10: transmittedVerticalPixelScaleField,
    11: compressionAlgorithmField,
    12: bitsPerPixelField,
    13: createField(
      "Friction ridge generalized position",
      "FGP",
      "Generalized finger position associated with the fingerprint image.",
      "Finger position code(s)"
    ),
    14: createField(
      "Print position descriptors",
      "PPD",
      "Descriptors such as tip, joint segment, or view qualifiers for the fingerprint image.",
      "Repeated descriptor items"
    ),
    15: createField(
      "Print position coordinates",
      "PPC",
      "Coordinates describing the positioned print region.",
      "Repeated coordinate values"
    ),
    18: createField(
      "Amputated or bandaged",
      "AMP",
      "Indicators that the finger is amputated, bandaged, or otherwise unavailable.",
      "Repeated condition codes"
    ),
    22: createField(
      "Fingerprint quality metric",
      "NQM",
      "Legacy fingerprint quality value retained for interchange with older profiles.",
      "Legacy quality metric"
    ),
    999: createOpaqueImageDataField("Binary fingerprint image payload.")
  },
  15: {
    2: imageDesignationCharacterField,
    3: createField("Impression type", "IMP", "Encoded impression or capture type.", "Impression code"),
    4: sourceAgencyField,
    5: createField("Capture date", "PCD", "Palm capture date when present.", "Calendar date in YYYYMMDD"),
    6: horizontalLineLengthField,
    7: verticalLineLengthField,
    8: scaleUnitsField,
    9: transmittedHorizontalPixelScaleField,
    10: transmittedVerticalPixelScaleField,
    11: compressionAlgorithmField,
    12: bitsPerPixelField,
    13: createField(
      "Friction ridge generalized position",
      "FGP",
      "Generalized palm or friction ridge position associated with the image.",
      "Palm or friction ridge position code(s)"
    ),
    999: createOpaqueImageDataField("Binary palmprint image payload.")
  },
  16: {
    2: imageDesignationCharacterField,
    3: createField(
      "User-defined image type",
      "UDI",
      "User-defined classification for the test image.",
      "User-defined type code"
    ),
    4: sourceAgencyField,
    5: createField(
      "Test capture date",
      "UTD",
      "Capture date for the test image when present.",
      "Calendar date in YYYYMMDD"
    ),
    6: horizontalLineLengthField,
    7: verticalLineLengthField,
    8: scaleUnitsField,
    9: transmittedHorizontalPixelScaleField,
    10: transmittedVerticalPixelScaleField,
    11: compressionAlgorithmField,
    12: bitsPerPixelField,
    13: createField(
      "Color space",
      "CSP",
      "Color space or channel interpretation used by the test image.",
      "Color space code"
    ),
    999: createOpaqueImageDataField("Binary test image payload.")
  },
  17: {
    2: imageDesignationCharacterField,
    3: createField("Eye label", "ELR", "Identifies which eye is represented.", "Eye label code"),
    4: sourceAgencyField,
    5: createField(
      "Iris capture date",
      "ICD",
      "Date that the iris image data was captured.",
      "Calendar date in YYYYMMDD"
    ),
    6: horizontalLineLengthField,
    7: verticalLineLengthField,
    8: scaleUnitsField,
    9: transmittedHorizontalPixelScaleField,
    10: transmittedVerticalPixelScaleField,
    11: compressionAlgorithmField,
    12: bitsPerPixelField,
    13: createField(
      "Color space",
      "CSP",
      "Color space or channel interpretation used by the iris image payload.",
      "Color space code"
    ),
    999: createOpaqueImageDataField("Binary iris image payload.")
  }
}

function humanizeFieldNumber(fieldNumber: number): string {
  return `Field ${fieldNumber.toString().padStart(3, "0")}`
}

export function getNistRecordMetadata(recordType: number): NistRecordMetadata {
  return (
    RECORD_METADATA[recordType] ?? {
      name: `Type-${recordType} record`,
      description: "Additional ANSI/NIST logical record."
    }
  )
}

export function getNistFieldMetadata(recordType: number, fieldNumber: number): NistFieldMetadata {
  return (
    FIELD_METADATA[recordType]?.[fieldNumber] ??
    GENERIC_FIELD_METADATA[fieldNumber] ?? {
      label: humanizeFieldNumber(fieldNumber),
      description: "Additional ANSI/NIST field exposed by the parser.",
      valueType: "Text or binary field content"
    }
  )
}
