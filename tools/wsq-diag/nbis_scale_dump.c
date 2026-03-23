#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <defs.h>

static const char *oracle_version = "NBIS Release 5.0.0";

static void print_scaled(const float value)
{
    char scale_ex = 0;
    unsigned short shrt_dat = 0;
    float flt_tmp = value;

    if (flt_tmp != 0.0f)
    {
        if (flt_tmp < 65535.0f)
        {
            while (flt_tmp < 65535.0f)
            {
                scale_ex += 1;
                flt_tmp *= 10.0f;
            }

            scale_ex -= 1;
            shrt_dat = (unsigned short)sround(flt_tmp / 10.0);
        }
        else
        {
            fprintf(stderr, "value too large: %f\n", value);
            exit(2);
        }
    }

    printf("input=%0.9f scale=%d raw=%u\n", value, (int)scale_ex, (unsigned int)shrt_dat);
}

int main(int argc, char **argv)
{
    if (argc == 2 && strcmp(argv[1], "--version") == 0)
    {
        puts(oracle_version);
        return 0;
    }

    if (argc != 2)
    {
        fprintf(stderr, "usage: %s <float>\n", argv[0]);
        return 1;
    }

    print_scaled(strtof(argv[1], NULL));
    return 0;
}
