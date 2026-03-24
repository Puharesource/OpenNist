#ifndef OPENNIST_FRFXLL_COMPAT_H
#define OPENNIST_FRFXLL_COMPAT_H

#include <cstddef>

typedef int FRFXLL_RESULT;

#define FRFXLL_OK ((FRFXLL_RESULT)0x00000000L)
#define FRFXLL_ERR_FB_TOO_SMALL_AREA ((FRFXLL_RESULT)0x80048004L)
#define FRFXLL_ERR_INVALID_PARAM ((FRFXLL_RESULT)0x80070057L)
#define FRFXLL_ERR_NO_MEMORY ((FRFXLL_RESULT)0x8007000EL)
#define FRFXLL_ERR_MORE_DATA ((FRFXLL_RESULT)0x800700EAL)
#define FRFXLL_ERR_INTERNAL ((FRFXLL_RESULT)0x8007054FL)
#define FRFXLL_ERR_INVALID_BUFFER ((FRFXLL_RESULT)0x8007007AL)
#define FRFXLL_ERR_INVALID_HANDLE ((FRFXLL_RESULT)0x80070006L)
#define FRFXLL_ERR_INVALID_IMAGE ((FRFXLL_RESULT)0x85BA0022L)
#define FRFXLL_ERR_INVALID_DATA ((FRFXLL_RESULT)0x85BA0024L)
#define FRFXLL_ERR_NO_FP ((FRFXLL_RESULT)0x85BA010AL)

#define FRFXLL_SUCCESS(rc) ((rc) >= FRFXLL_OK)

typedef void *FRFXLL_HANDLE;
typedef FRFXLL_HANDLE *FRFXLL_HANDLE_PT;

#define FRFXLL_FEX_ENABLE_ENHANCEMENT (0x00000002U)

enum FRXLL_MINUTIA_TYPE {
    OTHER = 0,
    RIDGE_END = 1,
    RIDGE_BIFURCATION = 2,
};

struct FRFXLL_Basic_19794_2_Minutia {
    unsigned short x;
    unsigned short y;
    unsigned char a;
    enum FRXLL_MINUTIA_TYPE t;
    unsigned char q;
};

enum FRXLL_MINUTIAE_LAYOUT {
    BASIC_19794_2_MINUTIA_STRUCT = 1,
};

extern "C" {
FRFXLL_RESULT FRFXLLCreateLibraryContext(FRFXLL_HANDLE_PT phContext);
FRFXLL_RESULT FRFXLLCloseHandle(FRFXLL_HANDLE_PT handle);
FRFXLL_RESULT FRFXLLCreateFeatureSetFromRaw(
    FRFXLL_HANDLE hContext,
    const unsigned char pixels[],
    size_t size,
    unsigned int width,
    unsigned int height,
    unsigned int imageResolution,
    unsigned int flags,
    FRFXLL_HANDLE_PT phFeatureSet);
FRFXLL_RESULT FRFXLLGetMinutiaInfo(
    const FRFXLL_HANDLE hFeatureSet,
    unsigned int *num_minutia,
    unsigned int *resolution_ppi);
FRFXLL_RESULT FRFXLLGetMinutiae(
    const FRFXLL_HANDLE mobj,
    enum FRXLL_MINUTIAE_LAYOUT layout,
    unsigned int *num_minutia,
    void *mdata);
}

#endif
