#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include <wsq.h>

int debug = 0;
static const char *oracle_version = "NBIS Release 5.0.0";

static void print_usage(const char *program_name)
{
    fprintf(stderr, "Usage: %s <raw-file> <width> <height> [stop-node] [row|col]\n", program_name);
}

int main(int argc, char **argv)
{
    if (argc == 2 && strcmp(argv[1], "--version") == 0)
    {
        puts(oracle_version);
        return 0;
    }

    if (argc < 4 || argc > 6)
    {
        print_usage(argv[0]);
        return 1;
    }

    const char *raw_path = argv[1];
    const int width = atoi(argv[2]);
    const int height = atoi(argv[3]);
    const int stop_node = argc >= 5 ? atoi(argv[4]) : W_TREELEN - 1;
    const int stop_after_row = argc == 6 && strcmp(argv[5], "row") == 0;
    const int pixel_count = width * height;

    if (stop_node < -1 || stop_node >= W_TREELEN)
    {
        fprintf(stderr, "Invalid stop node %d\n", stop_node);
        return 2;
    }

    FILE *raw_file = fopen(raw_path, "rb");
    if (raw_file == NULL)
    {
        perror("fopen");
        return 2;
    }

    unsigned char *raw_pixels = (unsigned char *)malloc((size_t)pixel_count);
    float *floating_pixels = (float *)malloc((size_t)pixel_count * sizeof(float));
    if (raw_pixels == NULL || floating_pixels == NULL)
    {
        fprintf(stderr, "malloc failed\n");
        free(floating_pixels);
        free(raw_pixels);
        fclose(raw_file);
        return 3;
    }

    const size_t bytes_read = fread(raw_pixels, 1, (size_t)pixel_count, raw_file);
    fclose(raw_file);
    if (bytes_read != (size_t)pixel_count)
    {
        fprintf(stderr, "Expected %d bytes but read %zu bytes\n", pixel_count, bytes_read);
        free(floating_pixels);
        free(raw_pixels);
        return 4;
    }

    float shift = 0.0f;
    float scale = 0.0f;
    if (conv_img_2_flt_ret(floating_pixels, &shift, &scale, raw_pixels, pixel_count) != 0)
    {
        fprintf(stderr, "conv_img_2_flt_ret failed\n");
        free(floating_pixels);
        free(raw_pixels);
        return 5;
    }

    build_wsq_trees(w_tree, W_TREELEN, q_tree, Q_TREELEN, width, height);

    float *temporary_pixels = (float *)malloc((size_t)pixel_count * sizeof(float));
    if (temporary_pixels == NULL)
    {
        fprintf(stderr, "malloc failed\n");
        free(floating_pixels);
        free(raw_pixels);
        return 6;
    }

    for (int node = 0; node <= stop_node; node++)
    {
        float *base_pixels = floating_pixels + (w_tree[node].y * width) + w_tree[node].x;
        get_lets(temporary_pixels, base_pixels, w_tree[node].leny, w_tree[node].lenx,
            width, 1, hifilt, MAX_HIFILT, lofilt, MAX_LOFILT, w_tree[node].inv_rw);

        if (stop_after_row && node == stop_node)
        {
            size_t values_written = 0;
            for (int row = 0; row < w_tree[node].leny; row++)
            {
                values_written += fwrite(
                    temporary_pixels + (row * width),
                    sizeof(float),
                    (size_t)w_tree[node].lenx,
                    stdout);
            }

            free(temporary_pixels);
            free(floating_pixels);
            free(raw_pixels);
            return values_written == (size_t)(w_tree[node].lenx * w_tree[node].leny) ? 0 : 7;
        }

        get_lets(base_pixels, temporary_pixels, w_tree[node].lenx, w_tree[node].leny,
            1, width, hifilt, MAX_HIFILT, lofilt, MAX_LOFILT, w_tree[node].inv_cl);
    }

    free(temporary_pixels);

    const size_t values_written = fwrite(floating_pixels, sizeof(float), (size_t)pixel_count, stdout);
    free(floating_pixels);
    free(raw_pixels);

    return values_written == (size_t)pixel_count ? 0 : 7;
}
