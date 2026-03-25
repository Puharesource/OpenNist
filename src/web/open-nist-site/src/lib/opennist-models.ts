export type WsqCommentInfo = {
  text: string;
  fields: Record<string, string>;
};

export type WsqFileInfo = {
  width: number;
  height: number;
  bitsPerPixel: number;
  pixelsPerInch: number;
  black: number;
  white: number;
  shift: number;
  scale: number;
  wsqEncoder: number;
  softwareImplementationNumber: number;
  highPassFilterLength: number;
  lowPassFilterLength: number;
  quantizationBinCenter: number;
  huffmanTableIds: number[];
  blockCount: number;
  encodedBlockByteCount: number;
  commentCount: number;
  nistCommentCount: number;
  comments: WsqCommentInfo[];
};

export type DecodedWsqDocument = {
  width: number;
  height: number;
  bitsPerPixel: number;
  pixelsPerInch: number;
  rawPixels: Uint8Array;
  fileInfo: WsqFileInfo;
};

export type NfiqAssessmentResult = {
  fingerCode: number;
  qualityScore: number;
  optionalError: string | null;
  quantized: boolean;
  resampled: boolean;
  actionableFeedback: Record<string, number | null>;
  nativeQualityMeasures: Record<string, number | null>;
  mappedQualityMeasures: Record<string, number | null>;
};

export type NistFieldInfo = {
  tag: string;
  value: string;
  subfieldCount: number;
  itemCount: number;
};

export type NistRecordInfo = {
  type: number;
  fieldCount: number;
  logicalRecordLength: number | null;
  byteOffset: number;
  encodedByteCount: number;
  isOpaqueBinaryRecord: boolean;
  fields: NistFieldInfo[];
};

export type NistFileInfo = {
  recordCount: number;
  version: string | null;
  contentSummary: string | null;
  records: NistRecordInfo[];
};

export type NistFieldInput = {
  tag: string;
  value: string;
};

export type NistRecordInput = {
  type: number;
  fields: NistFieldInput[];
};

export type NistFileInput = {
  records: NistRecordInput[];
};
