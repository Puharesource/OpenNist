#include <nfiq2_fingerprintimagedata.hpp>
#include <opencv2/imgcodecs.hpp>

#include "frfxll_compat.h"

#include <cstdint>
#include <exception>
#include <cstdio>
#include <iostream>
#include <memory>
#include <string>
#include <vector>

namespace {
constexpr uint32_t FingerJetMinWidth = 196;
constexpr uint32_t FingerJetMinHeight = 196;

NFIQ2::FingerprintImageData loadCroppedImage(const std::string &path)
{
    const auto image = cv::imread(path, cv::IMREAD_GRAYSCALE);
    if (image.empty()) {
        throw std::runtime_error("Failed to read image: " + path);
    }

    const auto pixelCount = static_cast<uint32_t>(image.total());
    NFIQ2::FingerprintImageData raw(
        image.ptr<uint8_t>(),
        pixelCount,
        static_cast<uint32_t>(image.cols),
        static_cast<uint32_t>(image.rows),
        0,
        NFIQ2::FingerprintImageData::Resolution500PPI);
    return raw.copyRemovingNearWhiteFrame();
}

FRFXLL_RESULT createContext(FRFXLL_HANDLE_PT phContext)
{
    FRFXLL_HANDLE hContext = nullptr;
    const auto result = FRFXLLCreateLibraryContext(&hContext);
    if (FRFXLL_SUCCESS(result)) {
        *phContext = hContext;
    }

    return result;
}
}

int main(int argc, char **argv)
{
    try {
        if (argc != 3) {
            std::cerr << "usage: nfiq2_minutiae_oracle <pgm-path> <command>\n";
            return 1;
        }

        const std::string imagePath = argv[1];
        const std::string command = argv[2];
        if (command != "minutiae") {
            std::cerr << "unsupported command: " << command << "\n";
            return 1;
        }

        const auto fingerprintImage = loadCroppedImage(imagePath);
        cv::Mat biggerImage;
        const auto imageTooSmall = fingerprintImage.width < FingerJetMinWidth
            || fingerprintImage.height < FingerJetMinHeight;
        if (imageTooSmall) {
            biggerImage = cv::Mat(
                std::max<uint32_t>(fingerprintImage.height, FingerJetMinHeight),
                std::max<uint32_t>(fingerprintImage.width, FingerJetMinWidth),
                CV_8UC1);
            biggerImage = static_cast<uint8_t>(255);
            const cv::Mat originalImage(
                static_cast<int>(fingerprintImage.height),
                static_cast<int>(fingerprintImage.width),
                CV_8UC1,
                const_cast<uint8_t *>(fingerprintImage.data()));
            originalImage.copyTo(biggerImage(cv::Rect(0, 0, fingerprintImage.width, fingerprintImage.height)));
        }

        FRFXLL_HANDLE hContext = nullptr;
        if (!FRFXLL_SUCCESS(createContext(&hContext)) || hContext == nullptr) {
            throw std::runtime_error("Failed to create FRFXLL context.");
        }

        const uint8_t *imageData = imageTooSmall ? biggerImage.ptr<uint8_t>() : fingerprintImage.data();
        const uint32_t imageWidth = imageTooSmall ? static_cast<uint32_t>(biggerImage.cols) : fingerprintImage.width;
        const uint32_t imageHeight = imageTooSmall ? static_cast<uint32_t>(biggerImage.rows) : fingerprintImage.height;
        const uint64_t imageSize = imageTooSmall ? static_cast<uint64_t>(imageWidth) * imageHeight : fingerprintImage.size();

        FRFXLL_HANDLE hFeatureSet = nullptr;
        const auto createFeatureSetResult = FRFXLLCreateFeatureSetFromRaw(
            hContext,
            imageData,
            imageSize,
            imageWidth,
            imageHeight,
            fingerprintImage.ppi,
            FRFXLL_FEX_ENABLE_ENHANCEMENT,
            &hFeatureSet);
        FRFXLLCloseHandle(&hContext);

        if (!FRFXLL_SUCCESS(createFeatureSetResult) || hFeatureSet == nullptr) {
            throw std::runtime_error("Failed to create FRFXLL feature set.");
        }

        unsigned int minutiaCount = 0;
        const auto minutiaInfoResult = FRFXLLGetMinutiaInfo(hFeatureSet, &minutiaCount, nullptr);
        if (!FRFXLL_SUCCESS(minutiaInfoResult)) {
            FRFXLLCloseHandle(&hFeatureSet);
            throw std::runtime_error("Failed to query minutia info.");
        }

        std::unique_ptr<FRFXLL_Basic_19794_2_Minutia[]> minutiae;
        if (minutiaCount > 0) {
            minutiae.reset(new FRFXLL_Basic_19794_2_Minutia[minutiaCount]);
            const auto minutiaeResult = FRFXLLGetMinutiae(
                hFeatureSet,
                BASIC_19794_2_MINUTIA_STRUCT,
                &minutiaCount,
                minutiae.get());
            if (!FRFXLL_SUCCESS(minutiaeResult)) {
                FRFXLLCloseHandle(&hFeatureSet);
                throw std::runtime_error("Failed to read minutiae.");
            }
        }

        FRFXLLCloseHandle(&hFeatureSet);

        std::printf("size %u %u\n", fingerprintImage.width, fingerprintImage.height);
        for (unsigned int index = 0; index < minutiaCount; index++) {
            const auto &minutia = minutiae[index];
            if (imageTooSmall
                && (minutia.x >= fingerprintImage.width || minutia.y >= fingerprintImage.height)) {
                continue;
            }

            std::printf(
                "minutia %u %u %u %u %u\n",
                static_cast<unsigned int>(minutia.x),
                static_cast<unsigned int>(minutia.y),
                static_cast<unsigned int>(minutia.a),
                static_cast<unsigned int>(minutia.q),
                static_cast<unsigned int>(minutia.t));
        }

        return 0;
    } catch (const std::exception &ex) {
        std::cerr << ex.what() << '\n';
        return 2;
    }
}
