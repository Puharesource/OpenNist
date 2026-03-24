#include <nfiq2_fingerprintimagedata.hpp>
#include <opencv2/imgcodecs.hpp>
#include <quality_modules/common_functions.h>

#include <cmath>
#include <cstdint>
#include <exception>
#include <iomanip>
#include <iostream>
#include <string>
#include <vector>

namespace {
constexpr int BlockSize = 32;
constexpr int SlantedBlockSizeX = 32;
constexpr int SlantedBlockSizeY = 16;
constexpr double Threshold = 0.1;
constexpr int ScannerResolution = 500;
constexpr double ScannerNormalizationBase = 125.0;
constexpr double RidgeWidthMaxAt125Ppi = 5.0;
constexpr double ValleyWidthMaxAt125Ppi = 5.0;
constexpr double RidgeWidthMin = 3.0;
constexpr double RidgeWidthMax = 10.0;
constexpr double ValleyWidthMin = 2.0;
constexpr double ValleyWidthMax = 10.0;

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

cv::Mat loadCroppedMat(const std::string &path)
{
    const auto cropped = loadCroppedImage(path);
    return cv::Mat(
        static_cast<int>(cropped.height),
        static_cast<int>(cropped.width),
        CV_8UC1,
        const_cast<uint8_t *>(cropped.data())).clone();
}

int getBlockOffset()
{
    const auto sumSquare = static_cast<double>((SlantedBlockSizeX * SlantedBlockSizeX) + (SlantedBlockSizeY * SlantedBlockSizeY));
    const auto extractedBlockSize = std::ceil(std::sqrt(sumSquare));
    const auto diff = extractedBlockSize - static_cast<double>(BlockSize);
    return static_cast<int>(std::ceil(diff / 2.0));
}

double computeBlockOrientation(const cv::Mat &image, int row, int column)
{
    const auto imageBlock = image(cv::Rect(column, row, BlockSize, BlockSize));
    double a = 0.0;
    double b = 0.0;
    double c = 0.0;
    NFIQ2::QualityMeasures::covcoef(
        imageBlock,
        a,
        b,
        c,
        NFIQ2::QualityMeasures::CENTERED_DIFFERENCES);
    return NFIQ2::QualityMeasures::ridgeorient(a, b, c);
}

cv::Mat extractOverlappingBlock(const cv::Mat &image, int row, int column)
{
    const auto blockOffset = getBlockOffset();
    return image(cv::Rect(
        column - blockOffset,
        row - blockOffset,
        BlockSize + (blockOffset * 2),
        BlockSize + (blockOffset * 2)));
}

void writeByteVector(const std::vector<uint8_t> &values)
{
    for (size_t index = 0; index < values.size(); index++) {
        if (index > 0) {
            std::cout << ' ';
        }

        std::cout << static_cast<int>(values[index]);
    }

    std::cout << '\n';
}

void writeDoubleVector(const std::vector<double> &values)
{
    for (size_t index = 0; index < values.size(); index++) {
        if (index > 0) {
            std::cout << ' ';
        }

        std::cout << values[index];
    }

    std::cout << '\n';
}

std::vector<uint8_t> copyMatBytes(const cv::Mat &matrix)
{
    std::vector<uint8_t> values(static_cast<size_t>(matrix.rows * matrix.cols));
    size_t destinationOffset = 0;
    for (int row = 0; row < matrix.rows; row++) {
        const auto *rowPtr = matrix.ptr<uint8_t>(row);
        std::memcpy(values.data() + destinationOffset, rowPtr, static_cast<size_t>(matrix.cols));
        destinationOffset += static_cast<size_t>(matrix.cols);
    }

    return values;
}

cv::Mat cropCenteredRotatedBlock(const cv::Mat &rotatedBlock)
{
    const auto halfWidth = SlantedBlockSizeX / 2;
    const auto halfHeight = SlantedBlockSizeY / 2;
    const auto center = rotatedBlock.rows / 2;
    return rotatedBlock(cv::Range(center - (halfHeight - 1) - 1, center + halfHeight),
        cv::Range(center - (halfWidth - 1) - 1, center + halfWidth));
}

void computeRidgeValleyStructure(
    const cv::Mat &blockCropped,
    std::vector<uint8_t> &ridval,
    std::vector<double> &dt)
{
    cv::Mat v3 = cv::Mat::zeros(blockCropped.cols, 1, CV_64F);
    for (int index = 0; index < blockCropped.cols; index++) {
        const auto columnMean = cv::mean(blockCropped.col(index), cv::noArray());
        v3.at<double>(index, 0) = columnMean.val[0];
    }

    cv::Mat dttemp(v3.rows, 2, CV_64F);
    for (int index = 0; index < v3.rows; index++) {
        dttemp.at<double>(index, 0) = 1;
        dttemp.at<double>(index, 1) = index + 1;
    }

    cv::Mat dt1;
    cv::solve(dttemp, v3, dt1, cv::DECOMP_QR);
    dt1.forEach<double>([](double &value, const int *) {
        value = round(value * 10000000000.0) / 10000000000.0;
    });

    for (int index = 0; index < v3.rows; index++) {
        const auto x = static_cast<double>(index + 1);
        const auto threshold = (x * dt1.at<double>(1, 0)) + dt1.at<double>(0, 0);
        dt.push_back(threshold);
        ridval.push_back(v3.at<double>(index, 0) < threshold ? 1 : 0);
    }
}

std::vector<int> computeChangeIndices(const std::vector<uint8_t> &ridval)
{
    std::vector<uint8_t> change;
    change.reserve(ridval.size());

    for (size_t index = 0; index < ridval.size(); index++) {
        const auto previousIndex = index == 0 ? ridval.size() - 1 : index - 1;
        change.push_back(ridval[index] != ridval[previousIndex] ? 1 : 0);
    }

    std::vector<int> changeIndex;
    for (size_t index = 1; index < change.size(); index++) {
        if (change[index] == 1) {
            changeIndex.push_back(static_cast<int>(index - 1));
        }
    }

    return changeIndex;
}

std::vector<double> computeRvupRatios(const std::vector<uint8_t> &ridval)
{
    const auto changeIndex = computeChangeIndices(ridval);
    if (changeIndex.empty()) {
        return {};
    }

    std::vector<uint8_t> ridvalComplete;
    for (int index = changeIndex.front() + 1; index < changeIndex.back(); index++) {
        ridvalComplete.push_back(ridval[static_cast<size_t>(index)]);
    }

    if (ridvalComplete.empty()) {
        return {};
    }

    std::vector<int> changeIndexComplete;
    for (size_t index = 1; index < changeIndex.size(); index++) {
        changeIndexComplete.push_back(changeIndex[index] - changeIndex.front());
    }

    if (changeIndexComplete.size() <= 1) {
        return {};
    }

    std::vector<int> changeComplete2;
    for (size_t index = changeIndexComplete.size() - 1; index > 0; index--) {
        changeComplete2.push_back(changeIndexComplete[index] - changeIndexComplete[index - 1]);
    }

    std::vector<double> ratios;
    if (changeComplete2.size() > 1) {
        for (size_t index = 0; index < changeComplete2.size() - 1; index++) {
            ratios.push_back(static_cast<double>(changeComplete2[index]) /
                static_cast<double>(changeComplete2[index + 1]));
        }

        const auto begrid = ridvalComplete.front();
        for (size_t index = begrid; index < ratios.size(); index += 2) {
            ratios[index] = 1.0 / ratios[index];
        }
    }

    return ratios;
}

double computeLocalClarity(
    const cv::Mat &blockCropped,
    const std::vector<uint8_t> &ridval,
    const std::vector<double> &dt)
{
    if (ridval.empty()) {
        return 0.0;
    }

    const auto changeIndex = computeChangeIndices(ridval);
    if (changeIndex.empty()) {
        return 0.0;
    }

    std::vector<int> widths;
    widths.push_back(changeIndex.front());
    for (size_t index = 1; index < changeIndex.size(); index++) {
        widths.push_back(changeIndex[index] - changeIndex[index - 1]);
    }

    const auto ridgeScale = (ScannerResolution / ScannerNormalizationBase) * RidgeWidthMaxAt125Ppi;
    const auto valleyScale = (ScannerResolution / ScannerNormalizationBase) * ValleyWidthMaxAt125Ppi;
    const auto normalizedRidgeMin = RidgeWidthMin / ridgeScale;
    const auto normalizedRidgeMax = RidgeWidthMax / ridgeScale;
    const auto normalizedValleyMin = ValleyWidthMin / ridgeScale;
    const auto normalizedValleyMax = ValleyWidthMax / ridgeScale;

    std::vector<double> ridgeWidths;
    std::vector<double> valleyWidths;
    if (ridval.front() == 1) {
        for (size_t index = 0; index < widths.size(); index += 2) {
            ridgeWidths.push_back(static_cast<double>(widths[index]) / ridgeScale);
        }

        for (size_t index = 0; index + 1 < widths.size(); index += 2) {
            valleyWidths.push_back(static_cast<double>(widths[index + 1]) / valleyScale);
        }
    } else {
        for (size_t index = 0; index < widths.size(); index += 2) {
            valleyWidths.push_back(static_cast<double>(widths[index]) / valleyScale);
        }

        for (size_t index = 0; index + 1 < widths.size(); index += 2) {
            ridgeWidths.push_back(static_cast<double>(widths[index + 1]) / ridgeScale);
        }
    }

    const auto ridgeMean = ridgeWidths.empty() ? 0.0 : cv::mean(ridgeWidths, cv::noArray()).val[0];
    const auto valleyMean = valleyWidths.empty() ? 0.0 : cv::mean(valleyWidths, cv::noArray()).val[0];
    if (ridgeMean < normalizedRidgeMin || ridgeMean > normalizedRidgeMax
        || valleyMean < normalizedValleyMin || valleyMean > normalizedValleyMax) {
        return 0.0;
    }

    int ridgeGood = 0;
    int valleyGood = 0;
    int ridgePixels = 0;
    int valleyPixels = 0;
    for (int column = 0; column < blockCropped.cols; column++) {
        if (ridval[static_cast<size_t>(column)] == 1) {
            const auto ridgeColumn = (blockCropped.col(column) >= dt[static_cast<size_t>(column)]);
            ridgeGood += countNonZero(ridgeColumn);
            ridgePixels += blockCropped.rows;
        } else {
            const auto valleyColumn = (blockCropped.col(column) < dt[static_cast<size_t>(column)]);
            valleyGood += countNonZero(valleyColumn);
            valleyPixels += blockCropped.rows;
        }
    }

    const auto alpha = static_cast<double>(valleyGood) / static_cast<double>(valleyPixels);
    const auto beta = static_cast<double>(ridgeGood) / static_cast<double>(ridgePixels);
    return 1.0 - ((alpha + beta) / 2.0);
}

double computeFrequencyDomainAnalysis(const cv::Mat &blockCropped)
{
    cv::Mat rowMeans = cv::Mat::zeros(blockCropped.rows, 1, CV_64F);
    for (int row = 0; row < blockCropped.rows; row++) {
        rowMeans.at<double>(row, 0) = cv::mean(blockCropped.row(row)).val[0];
    }

    cv::Mat tmp;
    const auto optimalRows = cv::getOptimalDFTSize(rowMeans.cols);
    const auto optimalColumns = cv::getOptimalDFTSize(rowMeans.rows);
    cv::copyMakeBorder(rowMeans.t(), tmp, 0, optimalRows - rowMeans.cols, 0, optimalColumns - rowMeans.rows,
        cv::BORDER_CONSTANT, cv::Scalar::all(0));

    cv::Mat planes[] = { tmp, cv::Mat::zeros(tmp.size(), CV_64F) };
    cv::Mat complex;
    cv::merge(planes, 2, complex);
    cv::dft(complex, complex, cv::DFT_COMPLEX_OUTPUT | cv::DFT_ROWS);

    cv::split(complex, planes);
    cv::magnitude(planes[0], planes[1], planes[0]);
    const cv::Mat absMagnitude = abs(planes[0]);
    const cv::Mat amplitude(absMagnitude, cv::Rect(1, 0, absMagnitude.cols - 1, 1));

    double maxValue;
    cv::Point maxLocation;
    cv::minMaxLoc(amplitude, nullptr, &maxValue, nullptr, &maxLocation);
    const cv::Mat amplitudeDenominator(amplitude, cv::Rect(0, 0, static_cast<int>(floor(amplitude.cols / 2.0)), 1));
    const auto denominator = cv::sum(amplitudeDenominator).val[0];

    if (maxLocation.x == 0 || maxLocation.x + 1 >= amplitude.cols) {
        return 1.0;
    }

    return (maxValue + (0.3 * (amplitude.at<double>(0, maxLocation.x - 1) + amplitude.at<double>(0, maxLocation.x + 1)))) /
        denominator;
}

}

int main(int argc, char **argv)
{
    try {
        std::cout << std::setprecision(17);

        if (argc != 3 && argc != 5) {
            std::cerr << "usage: nfiq2_common_oracle block-grid <pgm-path>\n";
            std::cerr << "   or: nfiq2_common_oracle rotated-block <pgm-path> <row> <column>\n";
            std::cerr << "   or: nfiq2_common_oracle ridge-structure <pgm-path> <row> <column>\n";
            std::cerr << "   or: nfiq2_common_oracle fda-block <pgm-path> <row> <column>\n";
            return 1;
        }

        const std::string command { argv[1] };
        const std::string path { argv[2] };
        const auto image = loadCroppedMat(path);

        if (command == "block-grid") {
            cv::Mat mask;
            NFIQ2::QualityMeasures::ridgesegment(image, BlockSize, Threshold, cv::noArray(), mask, cv::noArray());

            std::cout << "size " << image.cols << ' ' << image.rows << '\n';
            for (int row = 0; row < image.rows; row += BlockSize) {
                for (int column = 0; column < image.cols; column += BlockSize) {
                    const auto actualWidth = std::min(BlockSize, image.cols - column);
                    const auto actualHeight = std::min(BlockSize, image.rows - row);
                    if (actualWidth != BlockSize || actualHeight != BlockSize) {
                        continue;
                    }

                    const auto imageBlock = image(cv::Rect(column, row, actualWidth, actualHeight));
                    const auto maskBlock = mask(cv::Rect(column, row, actualWidth, actualHeight));

                    double a = 0.0;
                    double b = 0.0;
                    double c = 0.0;
                    NFIQ2::QualityMeasures::covcoef(
                        imageBlock,
                        a,
                        b,
                        c,
                        NFIQ2::QualityMeasures::CENTERED_DIFFERENCES);

                    const auto orientation = NFIQ2::QualityMeasures::ridgeorient(a, b, c);
                    const auto allNonZero = NFIQ2::QualityMeasures::allfun(maskBlock);

                    std::cout
                        << "block "
                        << row << ' '
                        << column << ' '
                        << static_cast<int>(allNonZero) << ' '
                        << orientation
                        << '\n';
                }
            }

            return 0;
        }

        if (argc != 5) {
            std::cerr << "missing row/column arguments for command: " << command << '\n';
            return 1;
        }

        const auto row = std::stoi(argv[3]);
        const auto column = std::stoi(argv[4]);
        const auto overlappingBlock = extractOverlappingBlock(image, row, column);
        const auto orientation = computeBlockOrientation(image, row, column);

        if (command == "rotated-block") {
            cv::Mat rotatedBlock;
            NFIQ2::QualityMeasures::getRotatedBlock(overlappingBlock, orientation, true, rotatedBlock);

            const auto sourceData = copyMatBytes(overlappingBlock);
            const auto data = copyMatBytes(rotatedBlock);

            std::cout << "size " << rotatedBlock.cols << ' ' << rotatedBlock.rows << '\n';
            std::cout << "orientation " << orientation << '\n';
            std::cout << "source ";
            writeByteVector(sourceData);
            std::cout << "data ";
            writeByteVector(data);
            return 0;
        }

        if (command == "ridge-structure") {
            cv::Mat rotatedBlock;
            NFIQ2::QualityMeasures::getRotatedBlock(overlappingBlock, orientation, true, rotatedBlock);
            const auto blockCropped = cropCenteredRotatedBlock(rotatedBlock);

            std::vector<uint8_t> ridval;
            std::vector<double> dt;
            computeRidgeValleyStructure(blockCropped, ridval, dt);
            const auto rvu = computeRvupRatios(ridval);
            const auto lcs = computeLocalClarity(blockCropped, ridval, dt);

            std::cout << "size " << blockCropped.cols << ' ' << blockCropped.rows << '\n';
            std::cout << "orientation " << orientation << '\n';
            std::cout << "data ";
            writeByteVector(copyMatBytes(blockCropped));
            std::cout << "dt ";
            writeDoubleVector(dt);
            std::cout << "ridval ";
            writeByteVector(ridval);
            std::cout << "rvu ";
            writeDoubleVector(rvu);
            std::cout << "lcs " << lcs << '\n';
            return 0;
        }

        if (command == "fda-block") {
            cv::Mat rotatedBlock;
            NFIQ2::QualityMeasures::getRotatedBlock(overlappingBlock, orientation + (M_PI / 2.0), true, rotatedBlock);
            const auto center = rotatedBlock.rows / 2;
            const auto halfWidth = SlantedBlockSizeX / 2;
            const auto halfHeight = SlantedBlockSizeY / 2;
            const auto blockCropped = rotatedBlock(
                cv::Range(center - (halfWidth - 1) - 1, center + halfWidth),
                cv::Range(center - (halfHeight - 1) - 1, center + halfHeight));

            std::cout << "size " << blockCropped.cols << ' ' << blockCropped.rows << '\n';
            std::cout << "orientation " << orientation << '\n';
            std::cout << "data ";
            writeByteVector(copyMatBytes(blockCropped));
            std::cout << "fda " << computeFrequencyDomainAnalysis(blockCropped) << '\n';
            return 0;
        }

        std::cerr << "unsupported command: " << command << "\n";
        return 1;
    } catch (const std::exception &ex) {
        std::cerr << ex.what() << '\n';
        return 2;
    }
}
