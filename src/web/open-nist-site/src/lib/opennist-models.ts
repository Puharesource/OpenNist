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
