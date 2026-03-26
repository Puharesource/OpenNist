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
  1: {
    label: "Logical record length",
    mnemonic: "LEN",
    description: "Byte length of the logical record as stored in the file.",
    valueType: "Unsigned integer byte count"
  }
}

const FIELD_METADATA: Record<number, Record<number, NistFieldMetadata>> = {
  1: {
    2: {
      label: "Version",
      mnemonic: "VER",
      description: "ANSI/NIST transaction version declared by the file.",
      valueType: "Version code string"
    },
    3: {
      label: "Transaction content",
      mnemonic: "CNT",
      description: "List of logical record types and IDC values present in the transaction.",
      valueType: "Repeated [record type, IDC] pairs"
    },
    4: {
      label: "Type of transaction",
      mnemonic: "TOT",
      description: "High-level transaction purpose or workflow code.",
      valueType: "Transaction code"
    },
    5: {
      label: "Date",
      mnemonic: "DAT",
      description: "Transaction date when present.",
      valueType: "Calendar date in YYYYMMDD"
    },
    7: {
      label: "Destination agency identifier",
      mnemonic: "DAI",
      description: "Receiving agency or system identifier.",
      valueType: "Agency identifier text"
    },
    8: {
      label: "Originating agency identifier",
      mnemonic: "ORI",
      description: "Submitting agency or system identifier.",
      valueType: "Agency identifier text"
    },
    9: {
      label: "Transaction control number",
      mnemonic: "TCN",
      description: "Unique transaction identifier used for correlation and tracking.",
      valueType: "Identifier text"
    },
    14: {
      label: "Domain-specific timestamp",
      mnemonic: "DTS",
      description: "UTC timestamp when present in Type-1 headers.",
      valueType: "Timestamp text"
    }
  },
  2: {
    2: {
      label: "Image designation character",
      mnemonic: "IDC",
      description: "Logical image slot or item reference within the transaction.",
      valueType: "Small unsigned integer"
    },
    3: {
      label: "Descriptive text",
      mnemonic: "TXT",
      description: "User-defined or profile-defined descriptive text.",
      valueType: "Free-form text"
    }
  },
  4: {
    2: { label: "Image designation character", mnemonic: "IDC", valueType: "Small unsigned integer" },
    3: { label: "Impression type", mnemonic: "IMP", valueType: "Impression code" },
    4: { label: "Finger position", mnemonic: "FGP", valueType: "Finger position code(s)" },
    5: { label: "Image scanning resolution", mnemonic: "ISR", valueType: "Resolution code" },
    6: { label: "Horizontal line length", mnemonic: "HLL", valueType: "Pixel width" },
    7: { label: "Vertical line length", mnemonic: "VLL", valueType: "Pixel height" },
    8: { label: "Grayscale compression algorithm", mnemonic: "GCA", valueType: "Compression code" },
    999: {
      label: "Image data",
      mnemonic: "DATA",
      description: "Binary fingerprint image payload preserved from the opaque record body.",
      valueType: "Binary image payload",
      imagePayload: true
    }
  },
  7: {
    2: { label: "Image designation character", mnemonic: "IDC", valueType: "Small unsigned integer" },
    3: { label: "Impression type", mnemonic: "IMP", valueType: "Impression code" },
    4: { label: "Finger position", mnemonic: "FGP", valueType: "Finger position code(s)" },
    5: { label: "Image scanning resolution", mnemonic: "ISR", valueType: "Resolution code" },
    6: { label: "Horizontal line length", mnemonic: "HLL", valueType: "Pixel width" },
    7: { label: "Vertical line length", mnemonic: "VLL", valueType: "Pixel height" },
    8: { label: "Compression algorithm", mnemonic: "CGA", valueType: "Compression code" },
    999: {
      label: "Image data",
      mnemonic: "DATA",
      description: "Binary user-defined image payload preserved from the opaque record body.",
      valueType: "Binary image payload",
      imagePayload: true
    }
  },
  8: {
    2: { label: "Image designation character", mnemonic: "IDC", valueType: "Small unsigned integer" },
    3: { label: "Signature type", mnemonic: "SIG", valueType: "Signature classification code" },
    4: { label: "Signature representation", mnemonic: "SRT", valueType: "Representation code" },
    5: { label: "Image scanning resolution", mnemonic: "ISR", valueType: "Resolution code" },
    6: { label: "Horizontal line length", mnemonic: "HLL", valueType: "Pixel width" },
    7: { label: "Vertical line length", mnemonic: "VLL", valueType: "Pixel height" },
    999: {
      label: "Image data",
      mnemonic: "DATA",
      description: "Binary signature payload.",
      valueType: "Binary image payload",
      imagePayload: true
    }
  },
  9: {
    2: { label: "Image designation character", mnemonic: "IDC", valueType: "Small unsigned integer" },
    3: { label: "Minutiae format", mnemonic: "FMT", valueType: "Format code" },
    4: { label: "Origin", mnemonic: "OFR", valueType: "Origin or source code" },
    5: { label: "Fingerprint pattern classification", mnemonic: "FPC", valueType: "Pattern code" },
    10: { label: "Minutiae data", mnemonic: "MIN", valueType: "Repeated minutiae item groups" }
  },
  10: {
    2: { label: "Image designation character", mnemonic: "IDC", valueType: "Small unsigned integer" },
    3: { label: "Image type", mnemonic: "IMT", valueType: "Image category code" },
    4: { label: "Source agency", mnemonic: "SRC", valueType: "Agency identifier text" },
    5: { label: "Capture date", mnemonic: "PHD", valueType: "Calendar date in YYYYMMDD" },
    6: { label: "Horizontal line length", mnemonic: "HLL", valueType: "Pixel width" },
    7: { label: "Vertical line length", mnemonic: "VLL", valueType: "Pixel height" },
    11: { label: "Compression algorithm", mnemonic: "CGA", valueType: "Compression code" },
    999: {
      label: "Image data",
      mnemonic: "DATA",
      description: "Binary face, scar, mark, or tattoo image payload.",
      valueType: "Binary image payload",
      imagePayload: true
    }
  },
  13: {
    2: { label: "Image designation character", mnemonic: "IDC", valueType: "Small unsigned integer" },
    3: { label: "Impression type", mnemonic: "IMP", valueType: "Impression code" },
    4: { label: "Source agency", mnemonic: "SRC", valueType: "Agency identifier text" },
    5: { label: "Capture date", mnemonic: "LCD", valueType: "Calendar date in YYYYMMDD" },
    6: { label: "Horizontal line length", mnemonic: "HLL", valueType: "Pixel width" },
    7: { label: "Vertical line length", mnemonic: "VLL", valueType: "Pixel height" },
    11: { label: "Compression algorithm", mnemonic: "CGA", valueType: "Compression code" },
    999: {
      label: "Image data",
      mnemonic: "DATA",
      description: "Binary latent fingerprint image payload.",
      valueType: "Binary image payload",
      imagePayload: true
    }
  },
  14: {
    2: { label: "Image designation character", mnemonic: "IDC", valueType: "Small unsigned integer" },
    3: { label: "Impression type", mnemonic: "IMP", valueType: "Impression code" },
    4: { label: "Source agency", mnemonic: "SRC", valueType: "Agency identifier text" },
    5: { label: "Capture date", mnemonic: "FCD", valueType: "Calendar date in YYYYMMDD" },
    6: { label: "Horizontal line length", mnemonic: "HLL", valueType: "Pixel width" },
    7: { label: "Vertical line length", mnemonic: "VLL", valueType: "Pixel height" },
    11: { label: "Compression algorithm", mnemonic: "CGA", valueType: "Compression code" },
    999: {
      label: "Image data",
      mnemonic: "DATA",
      description: "Binary fingerprint image payload.",
      valueType: "Binary image payload",
      imagePayload: true
    }
  },
  15: {
    2: { label: "Image designation character", mnemonic: "IDC", valueType: "Small unsigned integer" },
    3: { label: "Impression type", mnemonic: "IMP", valueType: "Impression code" },
    4: { label: "Source agency", mnemonic: "SRC", valueType: "Agency identifier text" },
    5: { label: "Capture date", mnemonic: "PCD", valueType: "Calendar date in YYYYMMDD" },
    6: { label: "Horizontal line length", mnemonic: "HLL", valueType: "Pixel width" },
    7: { label: "Vertical line length", mnemonic: "VLL", valueType: "Pixel height" },
    11: { label: "Compression algorithm", mnemonic: "CGA", valueType: "Compression code" },
    999: {
      label: "Image data",
      mnemonic: "DATA",
      description: "Binary palmprint image payload.",
      valueType: "Binary image payload",
      imagePayload: true
    }
  },
  16: {
    2: { label: "Image designation character", mnemonic: "IDC", valueType: "Small unsigned integer" },
    6: { label: "Horizontal line length", mnemonic: "HLL", valueType: "Pixel width" },
    7: { label: "Vertical line length", mnemonic: "VLL", valueType: "Pixel height" },
    11: { label: "Compression algorithm", mnemonic: "CGA", valueType: "Compression code" },
    999: {
      label: "Image data",
      mnemonic: "DATA",
      description: "Binary test image payload.",
      valueType: "Binary image payload",
      imagePayload: true
    }
  },
  17: {
    2: { label: "Image designation character", mnemonic: "IDC", valueType: "Small unsigned integer" },
    4: { label: "Source agency", mnemonic: "SRC", valueType: "Agency identifier text" },
    6: { label: "Horizontal line length", mnemonic: "HLL", valueType: "Pixel width" },
    7: { label: "Vertical line length", mnemonic: "VLL", valueType: "Pixel height" },
    11: { label: "Compression algorithm", mnemonic: "CGA", valueType: "Compression code" },
    999: {
      label: "Image data",
      mnemonic: "DATA",
      description: "Binary iris image payload.",
      valueType: "Binary image payload",
      imagePayload: true
    }
  }
}

function humanizeFieldNumber(fieldNumber: number): string {
  return `Field ${fieldNumber.toString().padStart(3, "0")}`
}

export function getNistRecordMetadata(recordType: number): NistRecordMetadata {
  return (
    RECORD_METADATA[recordType] ?? {
      name: `Type-${recordType} record`,
      description: "Record type not yet labeled in the built-in metadata catalog."
    }
  )
}

export function getNistFieldMetadata(recordType: number, fieldNumber: number): NistFieldMetadata {
  return (
    FIELD_METADATA[recordType]?.[fieldNumber] ??
    GENERIC_FIELD_METADATA[fieldNumber] ?? {
      label: humanizeFieldNumber(fieldNumber),
      valueType: "Profile-defined text or binary content"
    }
  )
}
