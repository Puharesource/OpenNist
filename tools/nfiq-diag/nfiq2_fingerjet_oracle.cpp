#include <opencv2/imgcodecs.hpp>

#include <algorithm>
#include <cstdint>
#include <fstream>
#include <iostream>
#include <limits>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>

#include "FeatureExtraction.h"
#include "orimap.h"
#include "serializeFpData.h"

namespace {
using namespace FingerJetFxOSE::FpRecEngineImpl::Embedded;

constexpr uint32_t FingerJetMinWidth = 196;
constexpr uint32_t FingerJetMinHeight = 196;
constexpr double NearWhiteThreshold = 250.0;

struct CroppedFingerprintImage
{
    cv::Mat image;
    int originalWidth;
    int originalHeight;
};

cv::Mat loadImage(const std::string &path)
{
    const auto image = cv::imread(path, cv::IMREAD_GRAYSCALE);
    if (image.empty()) {
        throw std::runtime_error("Failed to read image: " + path);
    }

    return image;
}

CroppedFingerprintImage loadCroppedImage(const std::string &path)
{
    const auto image = loadImage(path);

    auto topRowIndex = 0;
    auto bottomRowIndex = image.rows - 1;
    for (; topRowIndex < image.rows; ++topRowIndex) {
        if (cv::mean(image.row(topRowIndex))[0] <= NearWhiteThreshold) {
            break;
        }
    }

    if (topRowIndex >= image.rows) {
        throw std::runtime_error("All image rows appear to be blank.");
    }

    for (; bottomRowIndex >= topRowIndex; --bottomRowIndex) {
        if (cv::mean(image.row(bottomRowIndex))[0] <= NearWhiteThreshold) {
            break;
        }
    }

    if (bottomRowIndex <= 0) {
        bottomRowIndex = 0;
    }

    auto leftIndex = 0;
    auto rightIndex = image.cols - 1;
    for (; leftIndex < image.cols; ++leftIndex) {
        if (cv::mean(image.col(leftIndex))[0] <= NearWhiteThreshold) {
            break;
        }
    }

    if (leftIndex >= image.cols) {
        throw std::runtime_error("All image columns appear to be blank.");
    }

    for (; rightIndex >= leftIndex; --rightIndex) {
        if (cv::mean(image.col(rightIndex))[0] <= NearWhiteThreshold) {
            break;
        }
    }

    if (rightIndex <= 0) {
        rightIndex = 0;
    }

    if ((rightIndex <= leftIndex) || (bottomRowIndex <= topRowIndex)) {
        throw std::runtime_error("Invalid cropped bounds for fingerprint image.");
    }

    const auto roi = image(
        cv::Range(topRowIndex, bottomRowIndex + 1),
        cv::Range(leftIndex, rightIndex + 1));

    return CroppedFingerprintImage{
        roi.clone(),
        roi.cols,
        roi.rows,
    };
}

void writeBytes(const std::string &path, const uint8_t *data, size_t size)
{
    std::ofstream stream(path, std::ios::binary | std::ios::trunc);
    if (!stream) {
        throw std::runtime_error("Failed to open output file: " + path);
    }

    stream.write(reinterpret_cast<const char *>(data), static_cast<std::streamsize>(size));
    if (!stream) {
        throw std::runtime_error("Failed to write prepared image bytes: " + path);
    }
}

int runPrepareImage(const std::string &imagePath, int dpi, const std::string &outputPath)
{
    auto image = loadImage(imagePath);

    FeatureExtractionImpl::Parameters parameters;
    FeatureExtraction extractor(parameters);
    const auto initResult = extractor.Init(
        image.ptr<uint8_t>(),
        static_cast<size_t>(image.total()),
        static_cast<uint32_t>(image.cols),
        static_cast<uint32_t>(image.rows),
        static_cast<uint32_t>(dpi));
    if (initResult != FRFXLL_OK) {
        throw std::runtime_error("FeatureExtraction::Init failed with code " + std::to_string(initResult));
    }

    const auto resizeResult = extractor.Resize_AnyTo333InPlaceOrBuffer(
        extractor.imgIn,
        extractor.buffer,
        FeatureExtraction::maxsize);
    if (resizeResult != FRFXLL_OK) {
        throw std::runtime_error("FeatureExtraction::Resize_AnyTo333InPlaceOrBuffer failed with code " + std::to_string(resizeResult));
    }

    writeBytes(outputPath, extractor.buffer, extractor.size);
    std::cout << "size " << extractor.width << ' ' << extractor.height << '\n';
    std::cout << "resolution " << extractor.imageResolution << '\n';
    std::cout << "offsets " << extractor.xOffs << ' ' << extractor.yOffs << '\n';
    std::cout << "orientation " << extractor.ori_width << ' ' << extractor.ori_size << '\n';
    return 0;
}

int runRawMinutiae(const std::string &imagePath, int dpi)
{
    auto cropped = loadCroppedImage(imagePath);
    auto image = cropped.image;
    const auto imageTooSmall = image.cols < FingerJetMinWidth || image.rows < FingerJetMinHeight;
    if (imageTooSmall) {
        cv::Mat padded(
            std::max(image.rows, static_cast<int>(FingerJetMinHeight)),
            std::max(image.cols, static_cast<int>(FingerJetMinWidth)),
            CV_8UC1,
            cv::Scalar(255));
        image.copyTo(padded(cv::Rect(0, 0, image.cols, image.rows)));
        image = padded;
    }

    FeatureExtractionImpl::Parameters parameters;
    FeatureExtraction extractor(parameters);
    const auto initResult = extractor.Init(
        image.ptr<uint8_t>(),
        static_cast<size_t>(image.total()),
        static_cast<uint32_t>(image.cols),
        static_cast<uint32_t>(image.rows),
        static_cast<uint32_t>(dpi));
    if (initResult != FRFXLL_OK) {
        throw std::runtime_error("FeatureExtraction::Init failed with code " + std::to_string(initResult));
    }

    MatchData md;
    const auto resizeResult = extractor.Resize_AnyTo333InPlaceOrBuffer(
        extractor.imgIn,
        extractor.buffer,
        FeatureExtraction::maxsize);
    if (resizeResult != FRFXLL_OK) {
        throw std::runtime_error("FeatureExtraction::Resize_AnyTo333InPlaceOrBuffer failed with code " + std::to_string(resizeResult));
    }

    extractor.img = extractor.buffer;
    extractor.ori = reinterpret_cast<ori_t *>(extractor.img + extractor.size);
    const auto flags = FeatureExtractionBase::flag_enable_fft_enhancement;
    if ((flags & FeatureExtractionBase::flag_enable_fft_enhancement) != 0) {
        const auto enhBufferSize = extractor.size + std::max(extractor.ori_size * sizeof(ori_t), extractor.width << FeatureExtractionBase::enh_block_bits);
        if (enhBufferSize + extractor.ori_size > FeatureExtraction::maxsize) {
            throw std::runtime_error("Enhancement buffer exceeded FeatureExtraction::maxsize.");
        }

        extractor.footprint = extractor.img + enhBufferSize;
        orientation_map_and_footprint<FeatureExtractionBase::maxwidth, FeatureExtractionBase::ori_scale>(
            extractor.width,
            extractor.size,
            extractor.img,
            true,
            nullptr,
            extractor.footprint);
        if (!fft_enhance<FeatureExtractionBase::enh_block_bits, FeatureExtractionBase::enh_spacing>(
                extractor.img,
                extractor.width,
                extractor.size,
                enhBufferSize)) {
            throw std::runtime_error("fft_enhance failed.");
        }

        orientation_map_and_footprint<FeatureExtractionBase::maxwidth, FeatureExtractionBase::ori_scale>(
            extractor.width,
            extractor.size,
            extractor.img,
            false,
            extractor.ori,
            extractor.footprint);
    } else {
        extractor.footprint = extractor.img + extractor.size + extractor.ori_size * sizeof(ori_t);
        orientation_map_and_footprint<FeatureExtractionBase::maxwidth, FeatureExtractionBase::ori_scale>(
            extractor.width,
            extractor.size,
            extractor.img,
            true,
            extractor.ori,
            extractor.footprint);
    }

    freeman_phasemap<FeatureExtractionBase::ori_scale>(
        extractor.width,
        extractor.size,
        extractor.img,
        extractor.ori,
        extractor.img);

    FingerJetFxOSE::top_n<Minutia> topMinutia(md.minutia, md.minutia + md.capacity());
    extract_minutia<FeatureExtractionBase::maxwidth, FeatureExtractionBase::ori_scale>(
        extractor.img,
        extractor.width,
        extractor.size,
        extractor.footprint,
        topMinutia,
        extractor.param);
    md.numMinutia = topMinutia.size();
    topMinutia.sort();

    std::vector<size_t> emittedIndices;
    emittedIndices.reserve(md.numMinutia);
    for (size_t index = 0; index < md.numMinutia; index++) {
        const auto &minutia = md.minutia[index];
        auto finalX = FingerJetFxOSE::muldiv(
            static_cast<int16>(minutia.position.x * FeatureExtractionBase::imageScale / extractor.imageResolution)
                + static_cast<int16>(extractor.xOffs * FeatureExtractionBase::imageScale / extractor.imageResolution),
            197,
            167);
        auto finalY = FingerJetFxOSE::muldiv(
            static_cast<int16>(minutia.position.y * FeatureExtractionBase::imageScale / extractor.imageResolution)
                + static_cast<int16>(extractor.yOffs * FeatureExtractionBase::imageScale / extractor.imageResolution),
            197,
            167);
        if (imageTooSmall
            && (finalX >= cropped.originalWidth || finalY >= cropped.originalHeight)) {
            continue;
        }

        emittedIndices.push_back(index);
    }

    std::cout << "resolution " << extractor.imageResolution << '\n';
    std::cout << "offsets " << extractor.xOffs << ' ' << extractor.yOffs << '\n';
    std::cout << "size " << emittedIndices.size() << '\n';
    for (const auto index : emittedIndices) {
        const auto &minutia = md.minutia[index];
        std::cout
            << "raw "
            << minutia.position.x << ' '
            << minutia.position.y << ' '
            << static_cast<int>(minutia.theta) << ' '
            << static_cast<int>(minutia.conf) << ' '
            << static_cast<int>(minutia.type) << '\n';
    }

    return 0;
}

std::vector<uint8_t> parseBytes(const std::string &csv)
{
    std::vector<uint8_t> values;
    std::stringstream stream(csv);
    std::string token;
    while (std::getline(stream, token, ',')) {
        if (!token.empty()) {
            values.push_back(static_cast<uint8_t>(std::stoi(token)));
        }
    }

    return values;
}

std::vector<int> parseInts(const std::string &csv)
{
    std::vector<int> values;
    std::stringstream stream(csv);
    std::string token;
    while (std::getline(stream, token, ',')) {
        if (!token.empty()) {
            values.push_back(std::stoi(token));
        }
    }

    return values;
}

std::vector<bool> parseBools(const std::string &csv)
{
    std::vector<bool> values;
    std::stringstream stream(csv);
    std::string token;
    while (std::getline(stream, token, ',')) {
        if (!token.empty()) {
            values.push_back(token == "1");
        }
    }

    return values;
}

std::vector<std::complex<int8_t>> parseComplexPairs(const std::string &csv)
{
    std::vector<std::complex<int8_t>> values;
    std::stringstream stream(csv);
    std::string token;
    while (std::getline(stream, token, ';')) {
        if (token.empty()) {
            continue;
        }

        std::stringstream pairStream(token);
        std::string realToken;
        std::string imaginaryToken;
        if (!std::getline(pairStream, realToken, ',') || !std::getline(pairStream, imaginaryToken, ',')) {
            throw std::runtime_error("Invalid complex pair encoding.");
        }

        values.emplace_back(
            static_cast<int8_t>(std::stoi(realToken)),
            static_cast<int8_t>(std::stoi(imaginaryToken)));
    }

    return values;
}

void writeBytes(const std::vector<uint8_t> &values)
{
    for (size_t index = 0; index < values.size(); index++) {
        if (index > 0) {
            std::cout << ' ';
        }

        std::cout << static_cast<int>(values[index]);
    }

    std::cout << '\n';
}

void writeFlags(const std::vector<bool> &values)
{
    for (size_t index = 0; index < values.size(); index++) {
        if (index > 0) {
            std::cout << ' ';
        }

        std::cout << (values[index] ? 1 : 0);
    }

    std::cout << '\n';
}

void writeInts(const std::vector<int> &values)
{
    for (size_t index = 0; index < values.size(); index++) {
        if (index > 0) {
            std::cout << ' ';
        }

        std::cout << values[index];
    }

    std::cout << '\n';
}

void writeComplexPairs(const std::vector<std::complex<int8_t>> &values)
{
    for (size_t index = 0; index < values.size(); index++) {
        if (index > 0) {
            std::cout << ';';
        }

        std::cout << static_cast<int>(values[index].real()) << ',' << static_cast<int>(values[index].imag());
    }

    std::cout << '\n';
}

struct RawMinutiaRecord
{
    int x;
    int y;
    int angle;
    int confidence;
    int type;
};

int runOctSign(int real, int imaginary, int threshold)
{
    const auto result = oct_sign(
        std::complex<int32_t>(real, imaginary),
        threshold);
    std::cout << static_cast<int>(result.real()) << ' ' << static_cast<int>(result.imag()) << '\n';
    return 0;
}

int runDiv2(int real, int imaginary)
{
    const auto result = FeatureExtractionImpl::div2(std::complex<int32_t>(real, imaginary));
    std::cout << static_cast<int>(result.real()) << ' ' << static_cast<int>(result.imag()) << '\n';
    return 0;
}

int runFillHoles(int strideX, int sizeX, int strideY, int sizeY, const std::string &csv)
{
    auto values = parseBytes(csv);
    if (values.size() != static_cast<size_t>(sizeY)) {
        throw std::runtime_error("fill-holes byte count does not match sizeY.");
    }

    FeatureExtractionImpl::fill_holes(
        static_cast<size_t>(strideX),
        static_cast<size_t>(sizeX),
        static_cast<size_t>(strideY),
        static_cast<size_t>(sizeY),
        values.data());
    writeBytes(values);
    return 0;
}

template <int BoxSize>
int runBoxFilter(int width, int size, int threshold, const std::string &csv)
{
    auto values = parseBytes(csv);
    if (values.size() != static_cast<size_t>(size)) {
        throw std::runtime_error("box-filter byte count does not match size.");
    }

    if constexpr (BoxSize == 3) {
        FeatureExtractionImpl::boxfilt<64, 3>(
            static_cast<size_t>(width),
            static_cast<size_t>(size),
            values.data(),
            FeatureExtractionImpl::g<3>);
    } else if constexpr (BoxSize == 5) {
        if (threshold == 11) {
            FeatureExtractionImpl::boxfilt<64, 5>(
                static_cast<size_t>(width),
                static_cast<size_t>(size),
                values.data(),
                FeatureExtractionImpl::g<11>);
        } else if (threshold == 14) {
            FeatureExtractionImpl::boxfilt<64, 5>(
                static_cast<size_t>(width),
                static_cast<size_t>(size),
                values.data(),
                FeatureExtractionImpl::g<14>);
        } else {
            throw std::runtime_error("Unsupported threshold for 5x5 box filter.");
        }
    }

    writeBytes(values);
    return 0;
}

std::vector<RawMinutiaRecord> parseMinutiae(const std::string &csv)
{
    std::vector<RawMinutiaRecord> minutiae;
    std::stringstream rowStream(csv);
    std::string row;
    while (std::getline(rowStream, row, ';')) {
        if (row.empty()) {
            continue;
        }

        std::stringstream columnStream(row);
        std::string token;
        std::vector<int> columns;
        while (std::getline(columnStream, token, ',')) {
            columns.push_back(std::stoi(token));
        }

        if (columns.size() != 5) {
            throw std::runtime_error("Expected five columns per minutia.");
        }

        minutiae.push_back(RawMinutiaRecord{
            columns[0],
            columns[1],
            columns[2],
            columns[3],
            columns[4],
        });
    }

    return minutiae;
}

int mapMinutiaType(int type)
{
    switch (type) {
        case Minutia::type_ridge_ending:
            return RIDGE_END;
        case Minutia::type_bifurcation:
            return RIDGE_BIFURCATION;
        default:
            return OTHER;
    }
}

uint8_t clampByte(int value)
{
    value = std::max(0, std::min(value, static_cast<int>(std::numeric_limits<uint8_t>::max())));
    return static_cast<uint8_t>(value);
}

int runPostprocessMinutiae(
    int imageResolution,
    int xOffset,
    int yOffset,
    const std::string &csv)
{
    if (imageResolution <= 0) {
        throw std::runtime_error("imageResolution must be positive.");
    }

    auto records = parseMinutiae(csv);
    MatchData md;
    if (records.size() > md.capacity()) {
        throw std::runtime_error("Too many minutiae for MatchData capacity.");
    }

    md.numMinutia = records.size();
    for (size_t index = 0; index < records.size(); index++) {
        const auto &source = records[index];
        auto &target = md.minutia[index];
        target.position.x = static_cast<int16>(source.x);
        target.position.y = static_cast<int16>(source.y);
        target.theta = clampByte(source.angle);
        target.conf = clampByte(source.confidence);
        target.type = static_cast<uint8_t>(source.type);
    }

    for (size_t index = 0; index < md.numMinutia; index++) {
        auto &minutia = md.minutia[index];
        auto theta = static_cast<int>(minutia.theta);
        theta = -theta + 64;
        minutia.theta = static_cast<unsigned int>(theta);
    }

    for (size_t index = 0; index < md.numMinutia; index++) {
        auto &minutia = md.minutia[index];
        minutia.position.x = static_cast<int16>(minutia.position.x * FeatureExtractionBase::imageScale / imageResolution);
        minutia.position.y = static_cast<int16>(minutia.position.y * FeatureExtractionBase::imageScale / imageResolution);
    }

    const auto offsetX = static_cast<int16>(xOffset * FeatureExtractionBase::imageScale / imageResolution);
    const auto offsetY = static_cast<int16>(yOffset * FeatureExtractionBase::imageScale / imageResolution);
    for (size_t index = 0; index < md.numMinutia; index++) {
        auto &minutia = md.minutia[index];
        minutia.position.x += offsetX;
        minutia.position.y += offsetY;
    }

    for (size_t index = 0; index < md.numMinutia; index++) {
        auto &minutia = md.minutia[index];
        minutia.position.x = FingerJetFxOSE::muldiv(minutia.position.x, 197, 167);
        minutia.position.y = FingerJetFxOSE::muldiv(minutia.position.y, 197, 167);
    }

    std::cout << "size " << md.numMinutia << '\n';
    for (size_t index = 0; index < md.numMinutia; index++) {
        const auto &minutia = md.minutia[index];
        std::cout
            << "minutia "
            << minutia.position.x << ' '
            << minutia.position.y << ' '
            << static_cast<int>(minutia.theta) << ' '
            << static_cast<int>(StdFmdSerializer::QualityFromConfidence(minutia.conf)) << ' '
            << mapMinutiaType(minutia.type) << '\n';
    }

    return 0;
}

int runRankMinutiae(int capacity, const std::string &csv)
{
    if (capacity <= 0) {
        throw std::runtime_error("capacity must be positive.");
    }

    auto records = parseMinutiae(csv);
    std::vector<Minutia> storage(records.size());
    for (size_t index = 0; index < records.size(); index++) {
        const auto &source = records[index];
        auto &target = storage[index];
        target.position.x = static_cast<int16>(source.x);
        target.position.y = static_cast<int16>(source.y);
        target.theta = clampByte(source.angle);
        target.conf = clampByte(source.confidence);
        target.type = static_cast<uint8_t>(source.type);
    }

    const auto boundedCapacity = std::min<size_t>(records.size(), static_cast<size_t>(capacity));
    std::vector<Minutia> selected(boundedCapacity);
    FingerJetFxOSE::top_n<Minutia> topMinutia(selected.data(), selected.data() + boundedCapacity);
    for (const auto &minutia : storage) {
        topMinutia.add(minutia);
    }

    topMinutia.sort();
    std::cout << "size " << topMinutia.size() << '\n';
    for (size_t index = 0; index < topMinutia.size(); index++) {
        const auto &minutia = selected[index];
        std::cout
            << "raw "
            << minutia.position.x << ' '
            << minutia.position.y << ' '
            << static_cast<int>(minutia.theta) << ' '
            << static_cast<int>(minutia.conf) << ' '
            << static_cast<int>(minutia.type) << '\n';
    }

    return 0;
}

int runIsInFootprint(int x, int y, int width, int size, const std::string &csv)
{
    auto values = parseBytes(csv);
    if (values.size() != static_cast<size_t>(size)) {
        throw std::runtime_error("phasemap byte count does not match size.");
    }

    const auto result = FeatureExtractionImpl::is_in_footprint<1>(
        static_cast<size_t>(x),
        static_cast<size_t>(y),
        static_cast<size_t>(width),
        static_cast<size_t>(size),
        values.data());
    std::cout << (result ? 1 : 0) << '\n';
    return 0;
}

int runAdjustAngle(int x, int y, int width, int size, int angle, int relative, const std::string &csv)
{
    auto values = parseBytes(csv);
    if (values.size() != static_cast<size_t>(size)) {
        throw std::runtime_error("phasemap byte count does not match size.");
    }

    auto adjusted = clampByte(angle);
    const auto success = FeatureExtractionImpl::adjust_angle(
        adjusted,
        static_cast<size_t>(x),
        static_cast<size_t>(y),
        values.data(),
        static_cast<size_t>(width),
        static_cast<size_t>(size),
        relative != 0);
    std::cout << (success ? 1 : 0) << ' ' << static_cast<int>(adjusted) << '\n';
    return 0;
}

int runMax2D5Fast(int width, int size, const std::string &csv)
{
    auto values = parseInts(csv);
    if (values.size() != static_cast<size_t>(size)) {
        throw std::runtime_error("max2d5fast value count does not match size.");
    }

    FeatureExtractionImpl::max2d5fast<int32_t, 64> max5;
    std::vector<bool> output(values.size());
    for (int index = 0; index < size; index++) {
        const auto x = static_cast<size_t>(index % width);
        const auto y = static_cast<size_t>(index / width);
        output[static_cast<size_t>(index)] = max5(values[static_cast<size_t>(index)], x, y);
    }

    writeFlags(output);
    return 0;
}

template <int T0, int T1, int NormBits>
int runConv2D3(int width, int size, const std::string &csv)
{
    auto values = parseInts(csv);
    if (values.size() != static_cast<size_t>(size)) {
        throw std::runtime_error("conv2d3 value count does not match size.");
    }

    FeatureExtractionImpl::conv2d3<64, T0, T1, NormBits> convolution(static_cast<size_t>(width));
    std::vector<int> output(values.size());
    for (size_t index = 0; index < values.size(); index++) {
        output[index] = convolution(values[index]);
    }

    writeInts(output);
    return 0;
}

int runBoolDelay(int delayLength, int initialValue, const std::string &csv)
{
    auto values = parseBools(csv);
    FingerJetFxOSE::FpRecEngineImpl::Embedded::delay<bool, 64> delay(static_cast<size_t>(delayLength), initialValue != 0);
    std::vector<bool> output(values.size());
    for (size_t index = 0; index < values.size(); index++) {
        output[index] = delay(values[index]);
    }

    writeFlags(output);
    return 0;
}

int runDirectionAccumulator(int widthHalf, int rowCount, int filterSize, const std::string &csv)
{
    auto values = parseComplexPairs(csv);
    if (values.size() != static_cast<size_t>(widthHalf * rowCount)) {
        throw std::runtime_error("direction-accumulator value count does not match widthHalf * rowCount.");
    }

    std::vector<std::complex<int16_t>> verticalSums(static_cast<size_t>(widthHalf));
    std::vector<std::complex<int8_t>> verticalDelay(static_cast<size_t>(filterSize * widthHalf));
    size_t verticalDelayIndex = 0;
    std::vector<uint8_t> output(static_cast<size_t>(widthHalf * rowCount));
    for (int row = 0; row < rowCount; row++) {
        for (int x = 0; x < widthHalf; x++) {
            const auto value = values[static_cast<size_t>(row * widthHalf + x)];
            const auto delayed = verticalDelay[verticalDelayIndex];
            verticalDelay[verticalDelayIndex] = value;
            verticalDelayIndex++;
            if (verticalDelayIndex >= verticalDelay.size()) {
                verticalDelayIndex = 0;
            }
            auto &sum = verticalSums[static_cast<size_t>(x)];
            sum += value;
            sum -= delayed;
        }

        std::complex<int32_t> horizontalSum(0, 0);
        for (int x = 0; x < widthHalf + filterSize / 2; x++) {
            if (x < widthHalf) {
                horizontalSum += verticalSums[static_cast<size_t>(x)];
            }

            if (x >= filterSize) {
                horizontalSum -= verticalSums[static_cast<size_t>(x - filterSize)];
            }

            if (x >= filterSize / 2) {
                output[static_cast<size_t>(row * widthHalf + (x - filterSize / 2))] =
                    static_cast<uint8_t>(FingerJetFxOSE::atan2(horizontalSum.real(), horizontalSum.imag()) / 2);
            }
        }
    }

    writeBytes(output);
    return 0;
}

int runSmmeOrientationSequence(int width, int size, const std::string &csv)
{
    auto values = parseBytes(csv);
    if (values.size() != static_cast<size_t>(size)) {
        throw std::runtime_error("smme-orientation-sequence byte count does not match size.");
    }

    class Conv2D3Manual
    {
    public:
        Conv2D3Manual(size_t width, int32_t t0, int32_t t1, uint8_t normBits)
            : verticalDelay1(width)
            , verticalDelay2(width)
            , t0(t0)
            , t1(t1)
            , normBits(normBits)
        {
        }

        int32_t next(int32_t value)
        {
            const auto v1 = verticalDelay1[index1];
            verticalDelay1[index1] = value;
            index1 = (index1 + 1) % verticalDelay1.size();

            const auto v2 = verticalDelay2[index2];
            verticalDelay2[index2] = v1;
            index2 = (index2 + 1) % verticalDelay2.size();

            const auto h0 = v1 * t0 + (v2 + value) * t1;
            const auto h1 = horizontalDelay1;
            horizontalDelay1 = h0;
            const auto h2 = horizontalDelay2;
            horizontalDelay2 = h1;
            const auto output = h1 * t0 + (h2 + h0) * t1;
            return (output + (1 << (normBits - 1))) >> normBits;
        }

    private:
        std::vector<int32_t> verticalDelay1;
        std::vector<int32_t> verticalDelay2;
        size_t index1 = 0;
        size_t index2 = 0;
        int32_t horizontalDelay1 = 0;
        int32_t horizontalDelay2 = 0;
        int32_t t0;
        int32_t t1;
        uint8_t normBits;
    };

    static const size_t orientationFilterSize = 13;
    static const size_t yoffs = 3;
    static const uint8_t invalid = 255;

    const auto height = static_cast<size_t>(size / width);
    const auto endIndex = static_cast<size_t>(size - width);
    std::vector<std::complex<int8_t>> output;
    output.reserve((((height + orientationFilterSize) + 1) / 2) * (static_cast<size_t>(width) / 2));

    Conv2D3Manual cxx(static_cast<size_t>(width), 2, 1, 5);
    Conv2D3Manual cxy(static_cast<size_t>(width), 2, 1, 5);
    Conv2D3Manual cyy(static_cast<size_t>(width), 2, 1, 5);

    for (size_t y = 0; y < height + orientationFilterSize; y++) {
        for (int x = 0; x < width; x++) {
            const size_t pIndex = yoffs * static_cast<size_t>(width) + y * static_cast<size_t>(width) + static_cast<size_t>(x);
            bool outside = pIndex >= endIndex;
            int32_t gx = 0;
            int32_t gy = 0;
            if (!outside) {
                outside = values[pIndex + 1] == invalid
                    || values[pIndex - 3] == invalid
                    || values[pIndex + static_cast<size_t>(width)] == invalid
                    || values[pIndex - (3 * static_cast<size_t>(width))] == invalid;
                gx = static_cast<int32_t>(values[pIndex + 1]) - values[pIndex - 1];
                gy = static_cast<int32_t>(values[pIndex + static_cast<size_t>(width)]) - values[pIndex - static_cast<size_t>(width)];
            }

            const auto gxx = cxx.next(gx * gx);
            const auto gxy = cxy.next(gx * gy);
            const auto gyy = cyy.next(gy * gy);

            if ((x & 1) == 0 && (y & 1) == 0) {
                output.push_back(oct_sign(std::complex<int32_t>(gxx - gyy, 2 * gxy)));
            }
        }
    }

    writeComplexPairs(output);
    return 0;
}

int runBiffiltSample(int width, int size, int x, int y, const std::string &csv)
{
    auto values = parseBytes(csv);
    if (values.size() != static_cast<size_t>(size)) {
        throw std::runtime_error("biffilt-sample byte count does not match size.");
    }

    const FeatureExtractionImpl::image image(static_cast<size_t>(width), static_cast<size_t>(size), values.data());
    std::cout << static_cast<int>(image(static_cast<size_t>(x), static_cast<size_t>(y))) << '\n';
    return 0;
}

int runBiffiltEvaluate(int width, int size, int x, int y, int c, int s, const std::string &csv)
{
    auto values = parseBytes(csv);
    if (values.size() != static_cast<size_t>(size)) {
        throw std::runtime_error("biffilt-evaluate byte count does not match size.");
    }

    FeatureExtractionImpl::biffilt::parameters parameters;
    FeatureExtractionImpl::biffilt filter(
        static_cast<size_t>(width),
        static_cast<size_t>(size),
        values.data(),
        parameters);

    const auto confirmed = filter(
        static_cast<size_t>(x),
        static_cast<size_t>(y),
        static_cast<int8_t>(c),
        static_cast<int8_t>(s));

    std::cout
        << (confirmed ? 1 : 0) << ' '
        << (filter.type ? 1 : 0) << ' '
        << (filter.rotate180 ? 1 : 0) << ' '
        << filter.xoffs << ' '
        << filter.yoffs << ' '
        << filter.period << ' '
        << filter.confidence << '\n';
    return 0;
}

int runExtractMinutiaRaw(int width, int size, int capacity, const std::string &csv)
{
    if (capacity <= 0) {
        throw std::runtime_error("capacity must be positive.");
    }

    auto values = parseBytes(csv);
    if (values.size() != static_cast<size_t>(size)) {
        throw std::runtime_error("extract-minutia-raw byte count does not match size.");
    }

    std::vector<Minutia> selected(static_cast<size_t>(capacity));
    FingerJetFxOSE::top_n<Minutia> topMinutia(selected.data(), selected.data() + selected.size());
    FeatureExtractionImpl::Parameters parameters;
    extract_minutia<FeatureExtractionBase::maxwidth, FeatureExtractionBase::ori_scale>(
        values.data(),
        static_cast<size_t>(width),
        static_cast<size_t>(size),
        nullptr,
        topMinutia,
        parameters);
    topMinutia.sort();

    std::cout << "size " << topMinutia.size() << '\n';
    for (size_t index = 0; index < topMinutia.size(); index++) {
        const auto &minutia = selected[index];
        std::cout
            << "raw "
            << minutia.position.x << ' '
            << minutia.position.y << ' '
            << static_cast<int>(minutia.theta) << ' '
            << static_cast<int>(minutia.conf) << ' '
            << static_cast<int>(minutia.type) << '\n';
    }

    return 0;
}

template <class T>
struct PixelTraceSubscriber : public FingerJetFxOSE::Diagnostics::PixelsSubscriber<T> {
    std::vector<T> pixels;
    size_t width = 0;
    size_t height = 0;

    void SetSize(size_t width_, size_t height_) override
    {
        width = width_;
        height = height_;
        pixels.assign(width * height, T());
    }

    void SetPixel(size_t x, size_t y, const T &pixel) override
    {
        if (x < width && y < height) {
            pixels[(y * width) + x] = pixel;
        }
    }
};

int runExtractMinutiaTrace(
    int width,
    int size,
    int capacity,
    const std::string &csv,
    const std::string &directionOutputPath,
    const std::string &candidateOutputPath)
{
    if (capacity <= 0) {
        throw std::runtime_error("capacity must be positive.");
    }

    auto values = parseBytes(csv);
    if (values.size() != static_cast<size_t>(size)) {
        throw std::runtime_error("extract-minutia-trace byte count does not match size.");
    }

    std::vector<Minutia> selected(static_cast<size_t>(capacity));
    FingerJetFxOSE::top_n<Minutia> topMinutia(selected.data(), selected.data() + selected.size());
    FeatureExtractionImpl::Parameters parameters;
    PixelTraceSubscriber<bool> candidateTrace;
    PixelTraceSubscriber<uint8_t> directionTrace;
    parameters.smme.diagMC.ptr = &candidateTrace;
    parameters.smme.diagOri.ptr = &directionTrace;

    extract_minutia<FeatureExtractionBase::maxwidth, FeatureExtractionBase::ori_scale>(
        values.data(),
        static_cast<size_t>(width),
        static_cast<size_t>(size),
        nullptr,
        topMinutia,
        parameters);
    topMinutia.sort();

    std::ofstream directionOutput(directionOutputPath, std::ios::binary);
    if (!directionOutput) {
        throw std::runtime_error("Unable to open direction trace output file.");
    }

    directionOutput.write(
        reinterpret_cast<const char *>(directionTrace.pixels.data()),
        static_cast<std::streamsize>(directionTrace.pixels.size()));
    directionOutput.close();

    std::vector<uint8_t> candidateBytes(candidateTrace.pixels.size());
    for (size_t index = 0; index < candidateTrace.pixels.size(); index++) {
        candidateBytes[index] = candidateTrace.pixels[index] ? 1 : 0;
    }

    std::ofstream candidateOutput(candidateOutputPath, std::ios::binary);
    if (!candidateOutput) {
        throw std::runtime_error("Unable to open candidate trace output file.");
    }

    candidateOutput.write(
        reinterpret_cast<const char *>(candidateBytes.data()),
        static_cast<std::streamsize>(candidateBytes.size()));
    candidateOutput.close();

    std::cout << "size " << topMinutia.size() << '\n';
    for (size_t index = 0; index < topMinutia.size(); index++) {
        const auto &minutia = selected[index];
        std::cout
            << "raw "
            << minutia.position.x << ' '
            << minutia.position.y << ' '
            << static_cast<int>(minutia.theta) << ' '
            << static_cast<int>(minutia.conf) << ' '
            << static_cast<int>(minutia.type) << '\n';
    }

    return 0;
}

struct DebugMinutia
{
    Minutia minutia;
    uint8_t candidateAngle;
    bool adjustedAbsolute;
    bool adjustedRelative;
};

bool operator>(const DebugMinutia &left, const DebugMinutia &right)
{
    return CompareMinutiaByConfidence(left.minutia, right.minutia);
}

int runExtractMinutiaDebug(
    int width,
    int size,
    int capacity,
    const std::string &csv)
{
    if (capacity <= 0) {
        throw std::runtime_error("capacity must be positive.");
    }

    auto values = parseBytes(csv);
    if (values.size() != static_cast<size_t>(size)) {
        throw std::runtime_error("extract-minutia-debug byte count does not match size.");
    }

    static const uint8_t invalid = 255;
    static const int32_t invalidB = -1;
    static const size_t orifilt_size = 13;
    static const size_t yoffs = 3;
    static const int32_t Tb = 1328;

    const uint8_t *p = values.data() + static_cast<size_t>(width) * yoffs;
    const uint8_t *end = values.data() + static_cast<size_t>(size) - static_cast<size_t>(width);
    const size_t height = static_cast<size_t>(size / width);

    FeatureExtractionImpl::conv2d3<64, 2, 1, 5> cxx(static_cast<size_t>(width)), cxy(static_cast<size_t>(width)), cyy(static_cast<size_t>(width));
    using max5_t = FeatureExtractionImpl::max2d5fast<int32_t, 64>;
    max5_t max5;
    FingerJetFxOSE::FpRecEngineImpl::Embedded::delay<bool, 64 * (orifilt_size - max5_t::yoffs) - max5_t::xoffs> delayMc(static_cast<size_t>(width) * (orifilt_size - max5.yoffs) - max5.xoffs);
    FeatureExtractionImpl::biffilt bf(static_cast<size_t>(width), static_cast<size_t>(size), values.data(), FeatureExtractionImpl::Parameters().biffilt_);

    FingerJetFxOSE::FpRecEngineImpl::Embedded::delay<std::complex<int8_t>, orifilt_size * 64 / 2> delayOriY(orifilt_size * static_cast<size_t>(width) / 2);
    std::vector<std::complex<int16_t>> oriS1(static_cast<size_t>(width) / 2);
    std::vector<uint8_t> direction(static_cast<size_t>(width) / 2);
    std::vector<DebugMinutia> candidates;

    for (size_t y = 0; y < height + orifilt_size; y++) {
        for (size_t x = 0; x < static_cast<size_t>(width); p++, x++) {
            bool outside = p >= end;
            int32_t gx = 0;
            int32_t gy = 0;
            if (!outside) {
                outside = (p[1] == invalid) || (p[-3] == invalid) || (p[width] == invalid) || (p[-3 * width] == invalid);
                gx = int32_t(p[1]) - p[-1];
                gy = int32_t(p[width]) - p[-static_cast<int32_t>(width)];
            }

            int32_t gxx = gx * gx;
            int32_t gxy = gx * gy;
            int32_t gyy = gy * gy;
            gxx = cxx(gxx);
            gxy = cxy(gxy);
            gyy = cyy(gyy);

            bool e1b = gxx + gyy > 2 * Tb;
            int32_t b2 = (Tb - gxx) * (Tb - gyy) - gxy * gxy;
            bool e = e1b && b2 > 0;

            if ((x & 1) == 0 && (y & 1) == 0) {
                auto ori = oct_sign(std::complex<int32_t>(gxx - gyy, 2 * gxy));
                auto &s1 = oriS1[x / 2];
                s1 += ori;
                s1 -= delayOriY(ori);
            }

            bool mc = delayMc(max5(outside ? invalidB : e ? b2 : 0, x, y));
            size_t xp = x - cxx.xoffs;
            size_t yp = y + yoffs - cxx.yoffs - orifilt_size;
            mc = mc && FeatureExtractionImpl::is_in_footprint<1>(xp, yp, static_cast<size_t>(width), static_cast<size_t>(size), values.data());

            if (mc) {
                int confidence = 0;
                bool confirmed = false;
                bool type = false;
                uint8_t a = 0;
                for (int i = -2; i <= 2; i += 4) {
                    uint8_t a0 = direction[x / 2] + i;
                    int8_t c = cos(a0);
                    int8_t s = sin(a0);
                    if (bf(xp, yp, c, s)) {
                        confirmed = true;
                        if (bf.confidence > confidence) {
                            confidence = bf.confidence;
                            a = a0;
                            type = bf.type;
                            if (bf.rotate180) {
                                a = (a + 128) & 0xff;
                            }
                        }
                    }
                }

                if (confirmed) {
                    DebugMinutia debug {};
                    debug.minutia.position.x = int16(xp);
                    debug.minutia.position.y = int16(yp);
                    debug.candidateAngle = a;
                    debug.adjustedAbsolute = FeatureExtractionImpl::adjust_angle(a, xp, yp, values.data(), static_cast<size_t>(width), static_cast<size_t>(size), false);
                    debug.adjustedRelative = false;
                    if (!debug.adjustedAbsolute) {
                        debug.adjustedRelative = FeatureExtractionImpl::adjust_angle(a, xp, yp, values.data(), static_cast<size_t>(width), static_cast<size_t>(size), true);
                    }

                    debug.minutia.theta = a;
                    debug.minutia.conf = static_cast<uint8_t>(confidence);
                    debug.minutia.type = confidence > FeatureExtractionImpl::Parameters().biffilt_.type_thresold
                        ? (type ? Minutia::type_ridge_ending : Minutia::type_bifurcation)
                        : Minutia::type_other;
                    candidates.push_back(debug);
                }
            }
        }

        if ((y & 1) == 0) {
            std::complex<int32_t> s2(0);
            for (size_t x = 0; x < static_cast<size_t>(width) / 2 + orifilt_size / 2; x++) {
                s2 += x < static_cast<size_t>(width) / 2 ? oriS1[x] : 0;
                s2 -= x >= orifilt_size ? oriS1[x - orifilt_size] : 0;
                if (x >= orifilt_size / 2) {
                    direction[x - orifilt_size / 2] = static_cast<uint8_t>(FingerJetFxOSE::atan2(s2.real(), s2.imag()) / 2);
                }
            }
        }
    }

    std::sort(candidates.begin(), candidates.end(), std::greater<DebugMinutia>());
    if (candidates.size() > static_cast<size_t>(capacity)) {
        candidates.resize(static_cast<size_t>(capacity));
    }

    std::cout << "size " << candidates.size() << '\n';
    for (const auto &candidate : candidates) {
        std::cout
            << "dbg "
            << candidate.minutia.position.x << ' '
            << candidate.minutia.position.y << ' '
            << static_cast<int>(candidate.candidateAngle) << ' '
            << static_cast<int>(candidate.minutia.theta) << ' '
            << static_cast<int>(candidate.minutia.conf) << ' '
            << static_cast<int>(candidate.minutia.type) << ' '
            << (candidate.adjustedAbsolute ? 1 : 0) << ' '
            << (candidate.adjustedRelative ? 1 : 0)
            << '\n';
    }

    return 0;
}

int runPhasemap(const std::string &imagePath, int dpi, const std::string &outputPath)
{
    auto cropped = loadCroppedImage(imagePath);
    auto image = cropped.image;
    const auto imageTooSmall = image.cols < FingerJetMinWidth || image.rows < FingerJetMinHeight;
    if (imageTooSmall) {
        cv::Mat padded(
            std::max(image.rows, static_cast<int>(FingerJetMinHeight)),
            std::max(image.cols, static_cast<int>(FingerJetMinWidth)),
            CV_8UC1,
            cv::Scalar(255));
        image.copyTo(padded(cv::Rect(0, 0, image.cols, image.rows)));
        image = padded;
    }

    FeatureExtractionImpl::Parameters parameters;
    FeatureExtraction extractor(parameters);
    const auto initResult = extractor.Init(
        image.ptr<uint8_t>(),
        static_cast<size_t>(image.total()),
        static_cast<uint32_t>(image.cols),
        static_cast<uint32_t>(image.rows),
        static_cast<uint32_t>(dpi));
    if (initResult != FRFXLL_OK) {
        throw std::runtime_error("FeatureExtraction::Init failed with code " + std::to_string(initResult));
    }

    const auto resizeResult = extractor.Resize_AnyTo333InPlaceOrBuffer(
        extractor.imgIn,
        extractor.buffer,
        FeatureExtraction::maxsize);
    if (resizeResult != FRFXLL_OK) {
        throw std::runtime_error("FeatureExtraction::Resize_AnyTo333InPlaceOrBuffer failed with code " + std::to_string(resizeResult));
    }

    extractor.img = extractor.buffer;
    extractor.ori = reinterpret_cast<ori_t *>(extractor.img + extractor.size);
    const auto flags = FeatureExtractionBase::flag_enable_fft_enhancement;
    if ((flags & FeatureExtractionBase::flag_enable_fft_enhancement) != 0) {
        const auto enhBufferSize = extractor.size + std::max(extractor.ori_size * sizeof(ori_t), extractor.width << FeatureExtractionBase::enh_block_bits);
        if (enhBufferSize + extractor.ori_size > FeatureExtraction::maxsize) {
            throw std::runtime_error("Enhancement buffer exceeded FeatureExtraction::maxsize.");
        }

        extractor.footprint = extractor.img + enhBufferSize;
        orientation_map_and_footprint<FeatureExtractionBase::maxwidth, FeatureExtractionBase::ori_scale>(
            extractor.width,
            extractor.size,
            extractor.img,
            true,
            nullptr,
            extractor.footprint);
        if (!fft_enhance<FeatureExtractionBase::enh_block_bits, FeatureExtractionBase::enh_spacing>(
                extractor.img,
                extractor.width,
                extractor.size,
                enhBufferSize)) {
            throw std::runtime_error("fft_enhance failed.");
        }

        orientation_map_and_footprint<FeatureExtractionBase::maxwidth, FeatureExtractionBase::ori_scale>(
            extractor.width,
            extractor.size,
            extractor.img,
            false,
            extractor.ori,
            extractor.footprint);
    } else {
        extractor.footprint = extractor.img + extractor.size + extractor.ori_size * sizeof(ori_t);
        orientation_map_and_footprint<FeatureExtractionBase::maxwidth, FeatureExtractionBase::ori_scale>(
            extractor.width,
            extractor.size,
            extractor.img,
            true,
            extractor.ori,
            extractor.footprint);
    }

    freeman_phasemap<FeatureExtractionBase::ori_scale>(
        extractor.width,
        extractor.size,
        extractor.img,
        extractor.ori,
        extractor.img);

    std::ofstream output(outputPath, std::ios::binary);
    if (!output) {
        throw std::runtime_error("Unable to open phasemap output file.");
    }

    output.write(reinterpret_cast<const char *>(extractor.img), static_cast<std::streamsize>(extractor.size));
    output.close();

    std::cout << "size " << extractor.width << ' ' << (extractor.size / extractor.width) << '\n';
    std::cout << "resolution " << extractor.imageResolution << '\n';
    std::cout << "offsets " << extractor.xOffs << ' ' << extractor.yOffs << '\n';
    return 0;
}

int runEnhancedImage(const std::string &imagePath, int dpi, const std::string &outputPath)
{
    auto cropped = loadCroppedImage(imagePath);
    auto image = cropped.image;
    const auto imageTooSmall = image.cols < FingerJetMinWidth || image.rows < FingerJetMinHeight;
    if (imageTooSmall) {
        cv::Mat padded(
            std::max(image.rows, static_cast<int>(FingerJetMinHeight)),
            std::max(image.cols, static_cast<int>(FingerJetMinWidth)),
            CV_8UC1,
            cv::Scalar(255));
        image.copyTo(padded(cv::Rect(0, 0, image.cols, image.rows)));
        image = padded;
    }

    FeatureExtractionImpl::Parameters parameters;
    FeatureExtraction extractor(parameters);
    const auto initResult = extractor.Init(
        image.ptr<uint8_t>(),
        static_cast<size_t>(image.total()),
        static_cast<uint32_t>(image.cols),
        static_cast<uint32_t>(image.rows),
        static_cast<uint32_t>(dpi));
    if (initResult != FRFXLL_OK) {
        throw std::runtime_error("FeatureExtraction::Init failed with code " + std::to_string(initResult));
    }

    const auto resizeResult = extractor.Resize_AnyTo333InPlaceOrBuffer(
        extractor.imgIn,
        extractor.buffer,
        FeatureExtraction::maxsize);
    if (resizeResult != FRFXLL_OK) {
        throw std::runtime_error("FeatureExtraction::Resize_AnyTo333InPlaceOrBuffer failed with code " + std::to_string(resizeResult));
    }

    extractor.img = extractor.buffer;
    extractor.ori = reinterpret_cast<ori_t *>(extractor.img + extractor.size);
    const auto enhBufferSize = extractor.size + std::max(extractor.ori_size * sizeof(ori_t), extractor.width << FeatureExtractionBase::enh_block_bits);
    if (enhBufferSize + extractor.ori_size > FeatureExtraction::maxsize) {
        throw std::runtime_error("Enhancement buffer exceeded FeatureExtraction::maxsize.");
    }

    extractor.footprint = extractor.img + enhBufferSize;
    orientation_map_and_footprint<FeatureExtractionBase::maxwidth, FeatureExtractionBase::ori_scale>(
        extractor.width,
        extractor.size,
        extractor.img,
        true,
        nullptr,
        extractor.footprint);
    if (!fft_enhance<FeatureExtractionBase::enh_block_bits, FeatureExtractionBase::enh_spacing>(
            extractor.img,
            extractor.width,
            extractor.size,
            enhBufferSize)) {
        throw std::runtime_error("fft_enhance failed.");
    }

    std::ofstream output(outputPath, std::ios::binary);
    if (!output) {
        throw std::runtime_error("Unable to open enhanced image output file.");
    }

    output.write(reinterpret_cast<const char *>(extractor.img), static_cast<std::streamsize>(extractor.size));
    output.close();

    std::cout << "size " << extractor.width << ' ' << (extractor.size / extractor.width) << '\n';
    std::cout << "resolution " << extractor.imageResolution << '\n';
    std::cout << "offsets " << extractor.xOffs << ' ' << extractor.yOffs << '\n';
    std::cout << "orientation " << extractor.ori_width << ' ' << extractor.ori_size << '\n';
    return 0;
}

int runPhasemapSample(const std::string &imagePath, int dpi, int sampleIndex)
{
    auto cropped = loadCroppedImage(imagePath);
    auto image = cropped.image;
    const auto imageTooSmall = image.cols < FingerJetMinWidth || image.rows < FingerJetMinHeight;
    if (imageTooSmall) {
        cv::Mat padded(
            std::max(image.rows, static_cast<int>(FingerJetMinHeight)),
            std::max(image.cols, static_cast<int>(FingerJetMinWidth)),
            CV_8UC1,
            cv::Scalar(255));
        image.copyTo(padded(cv::Rect(0, 0, image.cols, image.rows)));
        image = padded;
    }

    FeatureExtractionImpl::Parameters parameters;
    FeatureExtraction extractor(parameters);
    const auto initResult = extractor.Init(
        image.ptr<uint8_t>(),
        static_cast<size_t>(image.total()),
        static_cast<uint32_t>(image.cols),
        static_cast<uint32_t>(image.rows),
        static_cast<uint32_t>(dpi));
    if (initResult != FRFXLL_OK) {
        throw std::runtime_error("FeatureExtraction::Init failed with code " + std::to_string(initResult));
    }

    const auto resizeResult = extractor.Resize_AnyTo333InPlaceOrBuffer(
        extractor.imgIn,
        extractor.buffer,
        FeatureExtraction::maxsize);
    if (resizeResult != FRFXLL_OK) {
        throw std::runtime_error("FeatureExtraction::Resize_AnyTo333InPlaceOrBuffer failed with code " + std::to_string(resizeResult));
    }

    extractor.img = extractor.buffer;
    extractor.ori = reinterpret_cast<ori_t *>(extractor.img + extractor.size);
    const auto flags = FeatureExtractionBase::flag_enable_fft_enhancement;
    if ((flags & FeatureExtractionBase::flag_enable_fft_enhancement) != 0) {
        const auto enhBufferSize = extractor.size + std::max(extractor.ori_size * sizeof(ori_t), extractor.width << FeatureExtractionBase::enh_block_bits);
        if (enhBufferSize + extractor.ori_size > FeatureExtraction::maxsize) {
            throw std::runtime_error("Enhancement buffer exceeded FeatureExtraction::maxsize.");
        }

        extractor.footprint = extractor.img + enhBufferSize;
        orientation_map_and_footprint<FeatureExtractionBase::maxwidth, FeatureExtractionBase::ori_scale>(
            extractor.width,
            extractor.size,
            extractor.img,
            true,
            nullptr,
            extractor.footprint);
        if (!fft_enhance<FeatureExtractionBase::enh_block_bits, FeatureExtractionBase::enh_spacing>(
                extractor.img,
                extractor.width,
                extractor.size,
                enhBufferSize)) {
            throw std::runtime_error("fft_enhance failed.");
        }

        orientation_map_and_footprint<FeatureExtractionBase::maxwidth, FeatureExtractionBase::ori_scale>(
            extractor.width,
            extractor.size,
            extractor.img,
            false,
            extractor.ori,
            extractor.footprint);
    } else {
        extractor.footprint = extractor.img + extractor.size + extractor.ori_size * sizeof(ori_t);
        orientation_map_and_footprint<FeatureExtractionBase::maxwidth, FeatureExtractionBase::ori_scale>(
            extractor.width,
            extractor.size,
            extractor.img,
            true,
            extractor.ori,
            extractor.footprint);
    }

    const size_t width = extractor.width;
    const size_t size = extractor.size;
    static const size_t ori_scale = FeatureExtractionBase::ori_scale;
    static const size_t ori_scale2 = ori_scale * ori_scale;
    const size_t ori_width = width / ori_scale;
    static const size_t flt_size2 = 4;
    const size_t computed_size = size - width * flt_size2 * 2;
    if (sampleIndex < 0 || static_cast<size_t>(sampleIndex) >= computed_size) {
        throw std::runtime_error("sampleIndex is outside computed phasemap range.");
    }

    const uint8 *p = extractor.img + (width + 1) * flt_size2;
    size_t currentIndex = 0;

    conv9<-112, -7, 48, 14, 1> x20;
    conv9<122, 78, 20, 2, 0> x22, x33;
    conv9<0, 71, 37, 6, 0> x21;
    conv9<0, -92, -12, 8, 1> x30;
    conv9<0, 52, 27, 4, 0> x32;
    conv9<-90, -23, 21, 7, 1> x31;

    for (size_t y = ori_width; y < size / ori_scale2 - ori_width; y += ori_width) {
        ori_t * pori = extractor.ori + y;
        for (size_t i = ori_scale; i; --i) {
            for (size_t x = 0; x < ori_width; ++x) {
                int32 a10 = pori[x].real();
                int32 a11 = pori[x].imag();

                int32 a20 = FingerJetFxOSE::sincosnorm(a10 * a10);
                int32 a21 = FingerJetFxOSE::sincosnorm(2 * a10 * a11);
                int32 a22 = FingerJetFxOSE::sincosnorm(a11 * a11);

                int32 a30 = FingerJetFxOSE::sincosnorm(a10 * a20);
                int32 a31 = FingerJetFxOSE::sincosnorm(3 * a20 * a11);
                int32 a32 = FingerJetFxOSE::sincosnorm(3 * a22 * a10);
                int32 a33 = FingerJetFxOSE::sincosnorm(a11 * a22);

                for (size_t j = flt_size2; j; ++p, --j, ++currentIndex) {
                    int32 v20 = x20(x22.vert(width, p));
                    int32 v21 = x21(x21.vert(width, p));
                    int32 v22v = x22(x20.vert(width, p));

                    int32 v30 = x30(x33.vert(width, p));
                    int32 v31v = x31(x32.vert(width, p));
                    int32 v32v = x32(x31.vert(width, p));
                    int32 v33v = x33(x30.vert(width, p));

                    int32 x2 = a20 * v20 + a21 * v21 + a22 * v22v;
                    int32 x3 = a30 * v30 + a31 * v31v + a32 * v32v + a33 * v33v;
                    x2 = (x2 + 1024) >> 11;
                    x3 = (x3 + 1024) >> 11;
                    uint8 out = x2 ? ((127 - oct_sign(complex<int32>(x2, x3)).real()) & 0xf0) : phasemap_filler;

                    if (currentIndex == static_cast<size_t>(sampleIndex)) {
                        std::cout << "orientation " << a10 << ' ' << a11 << '\n';
                        std::cout << "a2 " << a20 << ' ' << a21 << ' ' << a22 << '\n';
                        std::cout << "a3 " << a30 << ' ' << a31 << ' ' << a32 << ' ' << a33 << '\n';
                        std::cout << "v2 " << v20 << ' ' << v21 << ' ' << v22v << '\n';
                        std::cout << "v3 " << v30 << ' ' << v31v << ' ' << v32v << ' ' << v33v << '\n';
                        std::cout << "x " << x2 << ' ' << x3 << '\n';
                        std::cout << "out " << static_cast<int>(out) << '\n';
                        return 0;
                    }
                }
            }
        }
    }

    throw std::runtime_error("Requested phasemap sample was not reached.");
}

int runOrientationMap(
    const std::string &imagePath,
    int dpi,
    const std::string &orientationOutputPath,
    const std::string &footprintOutputPath)
{
    auto cropped = loadCroppedImage(imagePath);
    auto image = cropped.image;
    const auto imageTooSmall = image.cols < FingerJetMinWidth || image.rows < FingerJetMinHeight;
    if (imageTooSmall) {
        cv::Mat padded(
            std::max(image.rows, static_cast<int>(FingerJetMinHeight)),
            std::max(image.cols, static_cast<int>(FingerJetMinWidth)),
            CV_8UC1,
            cv::Scalar(255));
        image.copyTo(padded(cv::Rect(0, 0, image.cols, image.rows)));
        image = padded;
    }

    FeatureExtractionImpl::Parameters parameters;
    FeatureExtraction extractor(parameters);
    const auto initResult = extractor.Init(
        image.ptr<uint8_t>(),
        static_cast<size_t>(image.total()),
        static_cast<uint32_t>(image.cols),
        static_cast<uint32_t>(image.rows),
        static_cast<uint32_t>(dpi));
    if (initResult != FRFXLL_OK) {
        throw std::runtime_error("FeatureExtraction::Init failed with code " + std::to_string(initResult));
    }

    const auto resizeResult = extractor.Resize_AnyTo333InPlaceOrBuffer(
        extractor.imgIn,
        extractor.buffer,
        FeatureExtraction::maxsize);
    if (resizeResult != FRFXLL_OK) {
        throw std::runtime_error("FeatureExtraction::Resize_AnyTo333InPlaceOrBuffer failed with code " + std::to_string(resizeResult));
    }

    extractor.img = extractor.buffer;
    extractor.ori = reinterpret_cast<ori_t *>(extractor.img + extractor.size);
    extractor.footprint = extractor.img + extractor.size + extractor.ori_size * sizeof(ori_t);
    orientation_map_and_footprint<FeatureExtractionBase::maxwidth, FeatureExtractionBase::ori_scale>(
        extractor.width,
        extractor.size,
        extractor.img,
        true,
        extractor.ori,
        extractor.footprint);

    std::ofstream orientationOutput(orientationOutputPath, std::ios::binary);
    if (!orientationOutput) {
        throw std::runtime_error("Unable to open orientation output file.");
    }

    orientationOutput.write(
        reinterpret_cast<const char *>(extractor.ori),
        static_cast<std::streamsize>(extractor.ori_size * sizeof(ori_t)));
    orientationOutput.close();

    std::ofstream footprintOutput(footprintOutputPath, std::ios::binary);
    if (!footprintOutput) {
        throw std::runtime_error("Unable to open footprint output file.");
    }

    footprintOutput.write(
        reinterpret_cast<const char *>(extractor.footprint),
        static_cast<std::streamsize>(extractor.ori_size));
    footprintOutput.close();

    std::cout << "size " << extractor.width << ' ' << (extractor.size / extractor.width) << '\n';
    std::cout << "resolution " << extractor.imageResolution << '\n';
    std::cout << "offsets " << extractor.xOffs << ' ' << extractor.yOffs << '\n';
    std::cout << "orientation " << extractor.ori_width << ' ' << extractor.ori_size << '\n';
    return 0;
}

int runEnhancedOrientationMap(
    const std::string &imagePath,
    int dpi,
    const std::string &orientationOutputPath,
    const std::string &footprintOutputPath)
{
    auto cropped = loadCroppedImage(imagePath);
    auto image = cropped.image;
    const auto imageTooSmall = image.cols < FingerJetMinWidth || image.rows < FingerJetMinHeight;
    if (imageTooSmall) {
        cv::Mat padded(
            std::max(image.rows, static_cast<int>(FingerJetMinHeight)),
            std::max(image.cols, static_cast<int>(FingerJetMinWidth)),
            CV_8UC1,
            cv::Scalar(255));
        image.copyTo(padded(cv::Rect(0, 0, image.cols, image.rows)));
        image = padded;
    }

    FeatureExtractionImpl::Parameters parameters;
    FeatureExtraction extractor(parameters);
    const auto initResult = extractor.Init(
        image.ptr<uint8_t>(),
        static_cast<size_t>(image.total()),
        static_cast<uint32_t>(image.cols),
        static_cast<uint32_t>(image.rows),
        static_cast<uint32_t>(dpi));
    if (initResult != FRFXLL_OK) {
        throw std::runtime_error("FeatureExtraction::Init failed with code " + std::to_string(initResult));
    }

    const auto resizeResult = extractor.Resize_AnyTo333InPlaceOrBuffer(
        extractor.imgIn,
        extractor.buffer,
        FeatureExtraction::maxsize);
    if (resizeResult != FRFXLL_OK) {
        throw std::runtime_error("FeatureExtraction::Resize_AnyTo333InPlaceOrBuffer failed with code " + std::to_string(resizeResult));
    }

    extractor.img = extractor.buffer;
    extractor.ori = reinterpret_cast<ori_t *>(extractor.img + extractor.size);
    const auto enhBufferSize = extractor.size + std::max(extractor.ori_size * sizeof(ori_t), extractor.width << FeatureExtractionBase::enh_block_bits);
    if (enhBufferSize + extractor.ori_size > FeatureExtraction::maxsize) {
        throw std::runtime_error("Enhancement buffer exceeded FeatureExtraction::maxsize.");
    }

    extractor.footprint = extractor.img + enhBufferSize;
    orientation_map_and_footprint<FeatureExtractionBase::maxwidth, FeatureExtractionBase::ori_scale>(
        extractor.width,
        extractor.size,
        extractor.img,
        true,
        nullptr,
        extractor.footprint);
    if (!fft_enhance<FeatureExtractionBase::enh_block_bits, FeatureExtractionBase::enh_spacing>(
            extractor.img,
            extractor.width,
            extractor.size,
            enhBufferSize)) {
        throw std::runtime_error("fft_enhance failed.");
    }

    orientation_map_and_footprint<FeatureExtractionBase::maxwidth, FeatureExtractionBase::ori_scale>(
        extractor.width,
        extractor.size,
        extractor.img,
        false,
        extractor.ori,
        extractor.footprint);

    std::ofstream orientationOutput(orientationOutputPath, std::ios::binary);
    if (!orientationOutput) {
        throw std::runtime_error("Unable to open enhanced orientation output file.");
    }

    orientationOutput.write(
        reinterpret_cast<const char *>(extractor.ori),
        static_cast<std::streamsize>(extractor.ori_size * sizeof(ori_t)));
    orientationOutput.close();

    std::ofstream footprintOutput(footprintOutputPath, std::ios::binary);
    if (!footprintOutput) {
        throw std::runtime_error("Unable to open enhanced footprint output file.");
    }

    footprintOutput.write(
        reinterpret_cast<const char *>(extractor.footprint),
        static_cast<std::streamsize>(extractor.ori_size));
    footprintOutput.close();

    std::cout << "size " << extractor.width << ' ' << (extractor.size / extractor.width) << '\n';
    std::cout << "resolution " << extractor.imageResolution << '\n';
    std::cout << "offsets " << extractor.xOffs << ' ' << extractor.yOffs << '\n';
    std::cout << "orientation " << extractor.ori_width << ' ' << extractor.ori_size << '\n';
    return 0;
}

int runEnhancedRawOrientationMap(
    const std::string &imagePath,
    int dpi,
    const std::string &orientationOutputPath,
    const std::string &footprintOutputPath)
{
    auto cropped = loadCroppedImage(imagePath);
    auto image = cropped.image;
    const auto imageTooSmall = image.cols < FingerJetMinWidth || image.rows < FingerJetMinHeight;
    if (imageTooSmall) {
        cv::Mat padded(
            std::max(image.rows, static_cast<int>(FingerJetMinHeight)),
            std::max(image.cols, static_cast<int>(FingerJetMinWidth)),
            CV_8UC1,
            cv::Scalar(255));
        image.copyTo(padded(cv::Rect(0, 0, image.cols, image.rows)));
        image = padded;
    }

    FeatureExtractionImpl::Parameters parameters;
    FeatureExtraction extractor(parameters);
    const auto initResult = extractor.Init(
        image.ptr<uint8_t>(),
        static_cast<size_t>(image.total()),
        static_cast<uint32_t>(image.cols),
        static_cast<uint32_t>(image.rows),
        static_cast<uint32_t>(dpi));
    if (initResult != FRFXLL_OK) {
        throw std::runtime_error("FeatureExtraction::Init failed with code " + std::to_string(initResult));
    }

    const auto resizeResult = extractor.Resize_AnyTo333InPlaceOrBuffer(
        extractor.imgIn,
        extractor.buffer,
        FeatureExtraction::maxsize);
    if (resizeResult != FRFXLL_OK) {
        throw std::runtime_error("FeatureExtraction::Resize_AnyTo333InPlaceOrBuffer failed with code " + std::to_string(resizeResult));
    }

    extractor.img = extractor.buffer;
    extractor.ori = reinterpret_cast<ori_t *>(extractor.img + extractor.size);
    const auto enhBufferSize = extractor.size + std::max(extractor.ori_size * sizeof(ori_t), extractor.width << FeatureExtractionBase::enh_block_bits);
    if (enhBufferSize + extractor.ori_size > FeatureExtraction::maxsize) {
        throw std::runtime_error("Enhancement buffer exceeded FeatureExtraction::maxsize.");
    }

    extractor.footprint = extractor.img + enhBufferSize;
    orientation_map_and_footprint<FeatureExtractionBase::maxwidth, FeatureExtractionBase::ori_scale>(
        extractor.width,
        extractor.size,
        extractor.img,
        true,
        nullptr,
        extractor.footprint);
    if (!fft_enhance<FeatureExtractionBase::enh_block_bits, FeatureExtractionBase::enh_spacing>(
            extractor.img,
            extractor.width,
            extractor.size,
            enhBufferSize)) {
        throw std::runtime_error("fft_enhance failed.");
    }

    raw_orimap<FeatureExtractionBase::maxwidth, FeatureExtractionBase::ori_scale>(
        extractor.width,
        extractor.size,
        extractor.img,
        true,
        extractor.ori,
        extractor.footprint);

    std::ofstream orientationOutput(orientationOutputPath, std::ios::binary);
    if (!orientationOutput) {
        throw std::runtime_error("Unable to open enhanced raw orientation output file.");
    }

    orientationOutput.write(
        reinterpret_cast<const char *>(extractor.ori),
        static_cast<std::streamsize>(extractor.ori_size * sizeof(ori_t)));
    orientationOutput.close();

    std::ofstream footprintOutput(footprintOutputPath, std::ios::binary);
    if (!footprintOutput) {
        throw std::runtime_error("Unable to open enhanced raw footprint output file.");
    }

    footprintOutput.write(
        reinterpret_cast<const char *>(extractor.footprint),
        static_cast<std::streamsize>(extractor.ori_size));
    footprintOutput.close();

    std::cout << "size " << extractor.width << ' ' << (extractor.size / extractor.width) << '\n';
    std::cout << "resolution " << extractor.imageResolution << '\n';
    std::cout << "offsets " << extractor.xOffs << ' ' << extractor.yOffs << '\n';
    std::cout << "orientation " << extractor.ori_width << ' ' << extractor.ori_size << '\n';
    return 0;
}

int runRawOrientationMap(
    const std::string &imagePath,
    int dpi,
    const std::string &orientationOutputPath,
    const std::string &footprintOutputPath)
{
    auto cropped = loadCroppedImage(imagePath);
    auto image = cropped.image;
    const auto imageTooSmall = image.cols < FingerJetMinWidth || image.rows < FingerJetMinHeight;
    if (imageTooSmall) {
        cv::Mat padded(
            std::max(image.rows, static_cast<int>(FingerJetMinHeight)),
            std::max(image.cols, static_cast<int>(FingerJetMinWidth)),
            CV_8UC1,
            cv::Scalar(255));
        image.copyTo(padded(cv::Rect(0, 0, image.cols, image.rows)));
        image = padded;
    }

    FeatureExtractionImpl::Parameters parameters;
    FeatureExtraction extractor(parameters);
    const auto initResult = extractor.Init(
        image.ptr<uint8_t>(),
        static_cast<size_t>(image.total()),
        static_cast<uint32_t>(image.cols),
        static_cast<uint32_t>(image.rows),
        static_cast<uint32_t>(dpi));
    if (initResult != FRFXLL_OK) {
        throw std::runtime_error("FeatureExtraction::Init failed with code " + std::to_string(initResult));
    }

    const auto resizeResult = extractor.Resize_AnyTo333InPlaceOrBuffer(
        extractor.imgIn,
        extractor.buffer,
        FeatureExtraction::maxsize);
    if (resizeResult != FRFXLL_OK) {
        throw std::runtime_error("FeatureExtraction::Resize_AnyTo333InPlaceOrBuffer failed with code " + std::to_string(resizeResult));
    }

    extractor.img = extractor.buffer;
    extractor.ori = reinterpret_cast<ori_t *>(extractor.img + extractor.size);
    extractor.footprint = extractor.img + extractor.size + extractor.ori_size * sizeof(ori_t);
    raw_orimap<FeatureExtractionBase::maxwidth, FeatureExtractionBase::ori_scale>(
        extractor.width,
        extractor.size,
        extractor.img,
        true,
        extractor.ori,
        extractor.footprint);

    std::ofstream orientationOutput(orientationOutputPath, std::ios::binary);
    if (!orientationOutput) {
        throw std::runtime_error("Unable to open raw orientation output file.");
    }

    orientationOutput.write(
        reinterpret_cast<const char *>(extractor.ori),
        static_cast<std::streamsize>(extractor.ori_size * sizeof(ori_t)));
    orientationOutput.close();

    std::ofstream footprintOutput(footprintOutputPath, std::ios::binary);
    if (!footprintOutput) {
        throw std::runtime_error("Unable to open raw footprint output file.");
    }

    footprintOutput.write(
        reinterpret_cast<const char *>(extractor.footprint),
        static_cast<std::streamsize>(extractor.ori_size));
    footprintOutput.close();

    std::cout << "size " << extractor.width << ' ' << (extractor.size / extractor.width) << '\n';
    std::cout << "resolution " << extractor.imageResolution << '\n';
    std::cout << "offsets " << extractor.xOffs << ' ' << extractor.yOffs << '\n';
    std::cout << "orientation " << extractor.ori_width << ' ' << extractor.ori_size << '\n';
    return 0;
}

}

int main(int argc, char *argv[])
{
    try {
        if (argc < 2) {
            std::cerr << "Usage: nfiq2_fingerjet_oracle prepare-image <image> <dpi> <output>\n";
            return 2;
        }

        const std::string command = argv[1];
        if (command == "prepare-image") {
            if (argc != 5) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle prepare-image <image> <dpi> <output>\n";
                return 2;
            }

            return runPrepareImage(argv[2], std::stoi(argv[3]), argv[4]);
        }

        if (command == "raw-minutiae") {
            if (argc != 4) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle raw-minutiae <image> <dpi>\n";
                return 2;
            }

            return runRawMinutiae(argv[2], std::stoi(argv[3]));
        }

        if (command == "oct-sign") {
            if (argc != 5) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle oct-sign <real> <imag> <threshold>\n";
                return 2;
            }

            return runOctSign(std::stoi(argv[2]), std::stoi(argv[3]), std::stoi(argv[4]));
        }

        if (command == "div2") {
            if (argc != 4) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle div2 <real> <imag>\n";
                return 2;
            }

            return runDiv2(std::stoi(argv[2]), std::stoi(argv[3]));
        }

        if (command == "fill-holes") {
            if (argc != 7) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle fill-holes <strideX> <sizeX> <strideY> <sizeY> <csv>\n";
                return 2;
            }

            return runFillHoles(
                std::stoi(argv[2]),
                std::stoi(argv[3]),
                std::stoi(argv[4]),
                std::stoi(argv[5]),
                argv[6]);
        }

        if (command == "box-filter") {
            if (argc != 7) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle box-filter <boxSize> <width> <size> <threshold> <csv>\n";
                return 2;
            }

            const auto boxSize = std::stoi(argv[2]);
            const auto width = std::stoi(argv[3]);
            const auto size = std::stoi(argv[4]);
            const auto threshold = std::stoi(argv[5]);
            if (boxSize == 3) {
                return runBoxFilter<3>(width, size, threshold, argv[6]);
            }

            if (boxSize == 5) {
                return runBoxFilter<5>(width, size, threshold, argv[6]);
            }

            throw std::runtime_error("Unsupported box filter size.");
        }

        if (command == "postprocess-minutiae") {
            if (argc != 6) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle postprocess-minutiae <imageResolution> <xOffset> <yOffset> <csv>\n";
                return 2;
            }

            return runPostprocessMinutiae(
                std::stoi(argv[2]),
                std::stoi(argv[3]),
                std::stoi(argv[4]),
                argv[5]);
        }

        if (command == "rank-minutiae") {
            if (argc != 4) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle rank-minutiae <capacity> <csv>\n";
                return 2;
            }

            return runRankMinutiae(std::stoi(argv[2]), argv[3]);
        }

        if (command == "is-in-footprint") {
            if (argc != 7) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle is-in-footprint <x> <y> <width> <size> <csv>\n";
                return 2;
            }

            return runIsInFootprint(
                std::stoi(argv[2]),
                std::stoi(argv[3]),
                std::stoi(argv[4]),
                std::stoi(argv[5]),
                argv[6]);
        }

        if (command == "adjust-angle") {
            if (argc != 9) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle adjust-angle <x> <y> <width> <size> <angle> <relative> <csv>\n";
                return 2;
            }

            return runAdjustAngle(
                std::stoi(argv[2]),
                std::stoi(argv[3]),
                std::stoi(argv[4]),
                std::stoi(argv[5]),
                std::stoi(argv[6]),
                std::stoi(argv[7]),
                argv[8]);
        }

        if (command == "max2d5fast") {
            if (argc != 5) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle max2d5fast <width> <size> <csv>\n";
                return 2;
            }

            return runMax2D5Fast(
                std::stoi(argv[2]),
                std::stoi(argv[3]),
                argv[4]);
        }

        if (command == "conv2d3") {
            if (argc != 8) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle conv2d3 <width> <size> <t0> <t1> <normBits> <csv>\n";
                return 2;
            }

            const auto width = std::stoi(argv[2]);
            const auto size = std::stoi(argv[3]);
            const auto t0 = std::stoi(argv[4]);
            const auto t1 = std::stoi(argv[5]);
            const auto normBits = std::stoi(argv[6]);
            if (t0 == 2 && t1 == 1 && normBits == 5) {
                return runConv2D3<2, 1, 5>(width, size, argv[7]);
            }

            throw std::runtime_error("Unsupported conv2d3 parameterization.");
        }

        if (command == "bool-delay") {
            if (argc != 5) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle bool-delay <delayLength> <initialValue> <csv>\n";
                return 2;
            }

            return runBoolDelay(
                std::stoi(argv[2]),
                std::stoi(argv[3]),
                argv[4]);
        }

        if (command == "direction-accumulator") {
            if (argc != 6) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle direction-accumulator <widthHalf> <rowCount> <filterSize> <csv>\n";
                return 2;
            }

            return runDirectionAccumulator(
                std::stoi(argv[2]),
                std::stoi(argv[3]),
                std::stoi(argv[4]),
                argv[5]);
        }

        if (command == "smme-orientation-sequence") {
            if (argc != 5) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle smme-orientation-sequence <width> <size> <csv>\n";
                return 2;
            }

            return runSmmeOrientationSequence(
                std::stoi(argv[2]),
                std::stoi(argv[3]),
                argv[4]);
        }

        if (command == "biffilt-sample") {
            if (argc != 7) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle biffilt-sample <width> <size> <x> <y> <csv>\n";
                return 2;
            }

            return runBiffiltSample(
                std::stoi(argv[2]),
                std::stoi(argv[3]),
                std::stoi(argv[4]),
                std::stoi(argv[5]),
                argv[6]);
        }

        if (command == "biffilt-evaluate") {
            if (argc != 9) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle biffilt-evaluate <width> <size> <x> <y> <c> <s> <csv>\n";
                return 2;
            }

            return runBiffiltEvaluate(
                std::stoi(argv[2]),
                std::stoi(argv[3]),
                std::stoi(argv[4]),
                std::stoi(argv[5]),
                std::stoi(argv[6]),
                std::stoi(argv[7]),
                argv[8]);
        }

        if (command == "extract-minutia-trace") {
            if (argc != 8) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle extract-minutia-trace <width> <size> <capacity> <csv> <direction-output> <candidate-output>\n";
                return 2;
            }

            return runExtractMinutiaTrace(
                std::stoi(argv[2]),
                std::stoi(argv[3]),
                std::stoi(argv[4]),
                argv[5],
                argv[6],
                argv[7]);
        }

        if (command == "extract-minutia-debug") {
            if (argc != 6) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle extract-minutia-debug <width> <size> <capacity> <csv>\n";
                return 2;
            }

            return runExtractMinutiaDebug(
                std::stoi(argv[2]),
                std::stoi(argv[3]),
                std::stoi(argv[4]),
                argv[5]);
        }

        if (command == "extract-minutia-raw") {
            if (argc != 6) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle extract-minutia-raw <width> <size> <capacity> <csv>\n";
                return 2;
            }

            return runExtractMinutiaRaw(
                std::stoi(argv[2]),
                std::stoi(argv[3]),
                std::stoi(argv[4]),
                argv[5]);
        }

        if (command == "phasemap") {
            if (argc != 5) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle phasemap <image> <dpi> <output>\n";
                return 2;
            }

            return runPhasemap(argv[2], std::stoi(argv[3]), argv[4]);
        }

        if (command == "enhanced-image") {
            if (argc != 5) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle enhanced-image <image> <dpi> <output>\n";
                return 2;
            }

            return runEnhancedImage(argv[2], std::stoi(argv[3]), argv[4]);
        }

        if (command == "phasemap-sample") {
            if (argc != 5) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle phasemap-sample <image> <dpi> <sample-index>\n";
                return 2;
            }

            return runPhasemapSample(argv[2], std::stoi(argv[3]), std::stoi(argv[4]));
        }

        if (command == "orientation-map") {
            if (argc != 6) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle orientation-map <image> <dpi> <orientation-output> <footprint-output>\n";
                return 2;
            }

            return runOrientationMap(argv[2], std::stoi(argv[3]), argv[4], argv[5]);
        }

        if (command == "enhanced-orientation-map") {
            if (argc != 6) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle enhanced-orientation-map <image> <dpi> <orientation-output> <footprint-output>\n";
                return 2;
            }

            return runEnhancedOrientationMap(argv[2], std::stoi(argv[3]), argv[4], argv[5]);
        }

        if (command == "enhanced-raw-orientation-map") {
            if (argc != 6) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle enhanced-raw-orientation-map <image> <dpi> <orientation-output> <footprint-output>\n";
                return 2;
            }

            return runEnhancedRawOrientationMap(argv[2], std::stoi(argv[3]), argv[4], argv[5]);
        }

        if (command == "raw-orientation-map") {
            if (argc != 6) {
                std::cerr << "Usage: nfiq2_fingerjet_oracle raw-orientation-map <image> <dpi> <orientation-output> <footprint-output>\n";
                return 2;
            }

            return runRawOrientationMap(argv[2], std::stoi(argv[3]), argv[4], argv[5]);
        }

        std::cerr << "Unsupported command: " << command << '\n';
        return 2;
    } catch (const std::exception &ex) {
        std::cerr << ex.what() << '\n';
        return 1;
    }
}
