#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>

#include <wsq.h>
#include <defs.h>

int debug = 0;
static const char *oracle_version = "NBIS Release 5.0.0";

static void dump_quantize_trace(const QUANT_VALS *quant_vals)
{
    float A[NUM_SUBBANDS];
    float m[NUM_SUBBANDS];
    float sigma[NUM_SUBBANDS];
    float initial_qbss[NUM_SUBBANDS];
    int K0[NUM_SUBBANDS];
    int K1[NUM_SUBBANDS];
    int NP[NUM_SUBBANDS];
    int *K;
    int *nK;
    int K0len;
    int Klen;
    int nKlen;
    int NPlen;
    float S;
    float q;
    float P;
    int cnt;
    int i;

    for (cnt = 0; cnt < STRT_SUBBAND_3; cnt++)
        A[cnt] = 1.0f;
    A[cnt++] = 1.32f;
    A[cnt++] = 1.08f;
    A[cnt++] = 1.42f;
    A[cnt++] = 1.08f;
    A[cnt++] = 1.32f;
    A[cnt++] = 1.42f;
    A[cnt++] = 1.08f;
    A[cnt++] = 1.08f;

    for (cnt = 0; cnt < STRT_SIZE_REGION_2; cnt++)
        m[cnt] = 1.0f / 1024.0f;
    for (cnt = STRT_SIZE_REGION_2; cnt < STRT_SIZE_REGION_3; cnt++)
        m[cnt] = 1.0f / 256.0f;
    for (cnt = STRT_SIZE_REGION_3; cnt < NUM_SUBBANDS; cnt++)
        m[cnt] = 1.0f / 16.0f;

    K0len = 0;
    for (cnt = 0; cnt < NUM_SUBBANDS; cnt++) {
        if (quant_vals->var[cnt] < VARIANCE_THRESH) {
            initial_qbss[cnt] = 0.0f;
            continue;
        }

        sigma[cnt] = sqrt(quant_vals->var[cnt]);
        if (cnt < STRT_SIZE_REGION_2)
            initial_qbss[cnt] = 1.0f;
        else
            initial_qbss[cnt] = 10.0f / (A[cnt] * (float)log(quant_vals->var[cnt]));

        K0[K0len] = cnt;
        K1[K0len++] = cnt;
    }

    K = K1;
    Klen = K0len;

    while (1) {
        S = 0.0f;
        for (i = 0; i < Klen; i++)
            S += m[K[i]];

    P = 1.0f;
    for (i = 0; i < Klen; i++) {
        float ratio = sigma[K[i]] / initial_qbss[K[i]];
        double factor = pow(ratio, m[K[i]]);
        P *= factor;
        printf("trace.step[%d]=subband:%d ratio:%0.17g factor:%0.17g product:%0.17g\n",
            i, K[i], ratio, factor, P);
    }

        q = (pow(2, ((quant_vals->r / S) - 1.0)) / 2.5f) / pow(P, (1.0 / S));

        memset(NP, 0, NUM_SUBBANDS * sizeof(int));
        NPlen = 0;
        for (i = 0; i < Klen; i++) {
            if ((initial_qbss[K[i]] / q) >= (5.0f * sigma[K[i]])) {
                NP[K[i]] = TRUE;
                NPlen++;
            }
        }

        if (NPlen == 0)
            break;

        nK = K1;
        nKlen = 0;
        for (i = 0; i < Klen; i++) {
            if (!NP[K[i]])
                nK[nKlen++] = K[i];
        }

        K = nK;
        Klen = nKlen;
    }

    printf("trace.S=%0.17g\n", S);
    printf("trace.P=%0.17g\n", P);
    printf("trace.q=%0.17g\n", q);
    printf("trace.Klen=%d\n", Klen);
    for (i = 0; i < Klen; i++)
        printf("trace.active[%d]=%d\n", i, K[i]);
    for (cnt = 0; cnt < NUM_SUBBANDS; cnt++) {
        if (initial_qbss[cnt] != 0.0f)
            printf("trace.qprime[%d]=%0.17g\n", cnt, initial_qbss[cnt]);
    }
}

static void dump_quantization_table_scaling(const QUANT_VALS *quant_vals)
{
    int sub;
    char scale_ex;
    char scale_ex2;
    unsigned short shrt_dat;
    unsigned short shrt_dat2;
    float flt_tmp;

    for (sub = 0; sub < MAX_SUBBANDS; sub++)
    {
        if (sub < NUM_SUBBANDS && quant_vals->qbss[sub] != 0.0f)
        {
            flt_tmp = quant_vals->qbss[sub];
            scale_ex = 0;
            while (flt_tmp < 65535.0f)
            {
                scale_ex += 1;
                flt_tmp *= 10.0f;
            }
            scale_ex -= 1;
            shrt_dat = (unsigned short)sround(flt_tmp / 10.0);

            flt_tmp = quant_vals->qzbs[sub];
            scale_ex2 = 0;
            while (flt_tmp < 65535.0f)
            {
                scale_ex2 += 1;
                flt_tmp *= 10.0f;
            }
            scale_ex2 -= 1;
            shrt_dat2 = (unsigned short)sround(flt_tmp / 10.0);
        }
        else
        {
            scale_ex = 0;
            scale_ex2 = 0;
            shrt_dat = 0;
            shrt_dat2 = 0;
        }

        printf(
            "qtbl[%d]=qscale:%d qraw:%u zscale:%d zraw:%u\n",
            sub,
            (int)scale_ex,
            (unsigned int)shrt_dat,
            (int)scale_ex2,
            (unsigned int)shrt_dat2);
    }
}

static void dump_variance_trace(Q_TREE q_tree[], float *fip, const int width, const QUANT_VALS *quant_vals)
{
    float vsum = 0.0f;
    int subband;

    for (subband = 0; subband < 4; subband++)
        vsum += quant_vals->var[subband];

    for (subband = 0; subband < NUM_SUBBANDS; subband++)
    {
        const int use_cropped_region = (vsum >= 20000.0f) && (subband >= 4 || subband < 4);
        int startx = q_tree[subband].x;
        int starty = q_tree[subband].y;
        int lenx = q_tree[subband].lenx;
        int leny = q_tree[subband].leny;
        int skipx = 0;
        int skipy = 0;
        int row;
        int col;
        float *fp;
        float sum_pix = 0.0f;
        float ssq = 0.0f;
        float sum2;
        int sample_count;

        if (use_cropped_region)
        {
            skipx = q_tree[subband].lenx / 8;
            skipy = (9 * q_tree[subband].leny) / 32;
            startx += skipx;
            starty += skipy;
            lenx = (3 * q_tree[subband].lenx) / 4;
            leny = (7 * q_tree[subband].leny) / 16;
        }

        fp = fip + (starty * width) + startx;
        for (row = 0; row < leny; row++, fp += (width - lenx))
        {
            for (col = 0; col < lenx; col++)
            {
                sum_pix += *fp;
                ssq += *fp * *fp;
                fp++;
            }
        }

        sample_count = lenx * leny;
        sum2 = (sum_pix * sum_pix) / sample_count;

        printf(
            "vartrace[%d]=cropped:%d startx:%d starty:%d lenx:%d leny:%d skipx:%d skipy:%d samples:%d sum:%0.17g ssq:%0.17g sum2:%0.17g variance:%0.17g\n",
            subband,
            use_cropped_region,
            startx,
            starty,
            lenx,
            leny,
            skipx,
            skipy,
            sample_count,
            sum_pix,
            ssq,
            sum2,
            quant_vals->var[subband]);
    }
}

static void print_usage(const char *program_name)
{
    fprintf(stderr, "Usage: %s <raw-file> <width> <height> <bitrate>\n", program_name);
}

int main(int argc, char **argv)
{
    if (argc == 2 && strcmp(argv[1], "--version") == 0)
    {
        puts(oracle_version);
        return 0;
    }

    if (argc != 5 && argc != 7)
    {
        print_usage(argv[0]);
        return 1;
    }

    const char *raw_path = argv[1];
    const int width = atoi(argv[2]);
    const int height = atoi(argv[3]);
    const float bitrate = (float)atof(argv[4]);
    const int dump_coordinate_x = argc == 7 ? atoi(argv[5]) : -1;
    const int dump_coordinate_y = argc == 7 ? atoi(argv[6]) : -1;
    const int pixel_count = width * height;

    FILE *raw_file = fopen(raw_path, "rb");
    if (raw_file == NULL)
    {
        perror("fopen");
        return 2;
    }

    unsigned char *raw_pixels = (unsigned char *)malloc((size_t)pixel_count);
    if (raw_pixels == NULL)
    {
        fprintf(stderr, "malloc failed for raw pixels\n");
        fclose(raw_file);
        return 3;
    }

    const size_t bytes_read = fread(raw_pixels, 1, (size_t)pixel_count, raw_file);
    fclose(raw_file);
    if (bytes_read != (size_t)pixel_count)
    {
        fprintf(stderr, "Expected %d bytes but read %zu bytes\n", pixel_count, bytes_read);
        free(raw_pixels);
        return 4;
    }

    float *floating_pixels = (float *)malloc((size_t)pixel_count * sizeof(float));
    if (floating_pixels == NULL)
    {
        fprintf(stderr, "malloc failed for floating pixels\n");
        free(raw_pixels);
        return 5;
    }

    float shift = 0.0f;
    float scale = 0.0f;
    if (conv_img_2_flt_ret(floating_pixels, &shift, &scale, raw_pixels, pixel_count) != 0)
    {
        fprintf(stderr, "conv_img_2_flt_ret failed\n");
        free(floating_pixels);
        free(raw_pixels);
        return 6;
    }

    build_wsq_trees(w_tree, W_TREELEN, q_tree, Q_TREELEN, width, height);
    if (wsq_decompose(floating_pixels, width, height, w_tree, W_TREELEN, hifilt, MAX_HIFILT, lofilt, MAX_LOFILT) != 0)
    {
        fprintf(stderr, "wsq_decompose failed\n");
        free(floating_pixels);
        free(raw_pixels);
        return 7;
    }

    if (dump_coordinate_x >= 0 && dump_coordinate_y >= 0)
    {
        const int dump_index = dump_coordinate_y * width + dump_coordinate_x;
        printf("wavelet[%d,%d]=%0.17g\n", dump_coordinate_x, dump_coordinate_y, floating_pixels[dump_index]);
    }

    quant_vals.cr = 0.0f;
    quant_vals.q = 0.0f;
    quant_vals.r = bitrate;
    variance(&quant_vals, q_tree, Q_TREELEN, floating_pixels, width, height);
    dump_variance_trace(q_tree, floating_pixels, width, &quant_vals);
    dump_quantize_trace(&quant_vals);

    short *quantized_coefficients = NULL;
    int quantized_coefficient_count = 0;
    if (quantize(&quantized_coefficients, &quantized_coefficient_count, &quant_vals, q_tree, Q_TREELEN, floating_pixels, width, height) != 0)
    {
        fprintf(stderr, "quantize failed\n");
        free(floating_pixels);
        free(raw_pixels);
        return 8;
    }

    printf("shift=%0.17g\n", shift);
    printf("scale=%0.17g\n", scale);
    printf("qsize=%d\n", quantized_coefficient_count);

    for (int subband = 0; subband < NUM_SUBBANDS; subband++)
    {
        printf(
            "qtree[%d]=x:%d y:%d lenx:%d leny:%d\n",
            subband,
            q_tree[subband].x,
            q_tree[subband].y,
            q_tree[subband].lenx,
            q_tree[subband].leny);
    }

    for (int subband = 0; subband < NUM_SUBBANDS; subband++)
    {
        printf("var[%d]=%0.17g\n", subband, quant_vals.var[subband]);
        printf("qbin[%d]=%0.17g zbin[%d]=%0.17g\n", subband, quant_vals.qbss[subband], subband, quant_vals.qzbs[subband]);
    }
    dump_quantization_table_scaling(&quant_vals);

    for (int coefficient_index = 0; coefficient_index < quantized_coefficient_count; coefficient_index++)
    {
        printf("coeff[%d]=%d\n", coefficient_index, quantized_coefficients[coefficient_index]);
    }

    free(quantized_coefficients);
    free(floating_pixels);
    free(raw_pixels);
    return 0;
}
