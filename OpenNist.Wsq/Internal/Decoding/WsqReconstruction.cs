namespace OpenNist.Wsq.Internal.Decoding;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Major Code Smell",
    "S1244:Floating point numbers should not be tested for equality",
    Justification = "The WSQ inverse wavelet join mirrors the NBIS reference implementation, including exact zero checks on edge scale factors.")]
internal static class WsqReconstruction
{
    public static byte[] ReconstructToRawPixels(
        float[] waveletData,
        int width,
        int height,
        WsqWaveletNode[] waveletTree,
        WsqTransformTable transformTable,
        float shift,
        float scale)
    {
        ArgumentNullException.ThrowIfNull(waveletData);
        ArgumentNullException.ThrowIfNull(waveletTree);
        ArgumentNullException.ThrowIfNull(transformTable);

        ReconstructToFloatingPointPixels(waveletData, width, height, waveletTree, transformTable);
        return ConvertToBytePixels(waveletData, width, height, shift, scale);
    }

    internal static float[] ReconstructToFloatingPointPixels(
        float[] waveletData,
        int width,
        int height,
        WsqWaveletNode[] waveletTree,
        WsqTransformTable transformTable)
    {
        ArgumentNullException.ThrowIfNull(waveletData);
        ArgumentNullException.ThrowIfNull(waveletTree);
        ArgumentNullException.ThrowIfNull(transformTable);

        var lowPassFilter = transformTable.LowPassFilterCoefficients.ToArray();
        var highPassFilter = transformTable.HighPassFilterCoefficients.ToArray();
        var temporaryBuffer = new float[waveletData.Length];

        for (var nodeIndex = waveletTree.Length - 1; nodeIndex >= 0; nodeIndex--)
        {
            var node = waveletTree[nodeIndex];
            var baseOffset = (node.Y * width) + node.X;

            JoinLets(
                temporaryBuffer,
                0,
                waveletData,
                baseOffset,
                node.Width,
                node.Height,
                1,
                width,
                highPassFilter,
                lowPassFilter,
                node.InvertColumns);

            JoinLets(
                waveletData,
                baseOffset,
                temporaryBuffer,
                0,
                node.Height,
                node.Width,
                width,
                1,
                highPassFilter,
                lowPassFilter,
                node.InvertRows);
        }

        return waveletData;
    }

    private static unsafe void JoinLets(
        float[] destination,
        int destinationBaseOffset,
        float[] source,
        int sourceBaseOffset,
        int len1,
        int len2,
        int pitch,
        int stride,
        float[] highPassFilter,
        float[] lowPassFilter,
        bool invert)
    {
        fixed (float* destinationStart = destination)
        fixed (float* sourceStart = source)
        fixed (float* highPassStart = highPassFilter)
        fixed (float* lowPassStart = lowPassFilter)
        {
            float* @new = destinationStart + destinationBaseOffset;
            float* old = sourceStart + sourceBaseOffset;
            float* hi = highPassStart;
            float* lo = lowPassStart;

            float* lp0;
            float* lp1;
            float* hp0;
            float* hp1;
            float* lopass;
            float* hipass;
            float* limg;
            float* himg;
            int clRw;
            int i;
            var daEv = len2 % 2;
            int loc;
            int hoc;
            int hlen;
            int llen;
            var nstr = -stride;
            var pstr = stride;
            int tap;
            var fiEv = lowPassFilter.Length % 2;
            int olle;
            int ohle;
            int olre;
            int ohre;
            int lle;
            int lle2;
            int lre;
            int lre2;
            int hle;
            int hle2;
            int hre;
            int hre2;
            float* lpx;
            float* lspx;
            int lpxstr;
            int lspxstr;
            int lstap;
            int lotap;
            float* hpx;
            float* hspx;
            int hpxstr;
            int hspxstr;
            int hstap;
            int hotap;
            int asym;
            var fhre = 0;
            int ofhre;
            float ssfac;
            float osfac;
            float sfac;

            if (daEv != 0)
            {
                llen = (len2 + 1) / 2;
                hlen = llen - 1;
            }
            else
            {
                llen = len2 / 2;
                hlen = llen;
            }

            if (fiEv != 0)
            {
                asym = 0;
                ssfac = 1.0f;
                ofhre = 0;
                loc = (lowPassFilter.Length - 1) / 4;
                hoc = ((highPassFilter.Length + 1) / 4) - 1;
                lotap = ((lowPassFilter.Length - 1) / 2) % 2;
                hotap = ((highPassFilter.Length + 1) / 2) % 2;

                if (daEv != 0)
                {
                    olle = 0;
                    olre = 0;
                    ohle = 1;
                    ohre = 1;
                }
                else
                {
                    olle = 0;
                    olre = 1;
                    ohle = 1;
                    ohre = 0;
                }
            }
            else
            {
                asym = 1;
                ssfac = -1.0f;
                ofhre = 2;
                loc = (lowPassFilter.Length / 4) - 1;
                hoc = (highPassFilter.Length / 4) - 1;
                lotap = (lowPassFilter.Length / 2) % 2;
                hotap = (highPassFilter.Length / 2) % 2;

                if (daEv != 0)
                {
                    olle = 1;
                    olre = 0;
                    ohle = 1;
                    ohre = 1;
                }
                else
                {
                    olle = 1;
                    olre = 1;
                    ohle = 1;
                    ohre = 1;
                }

                if (loc == -1)
                {
                    loc = 0;
                    olle = 0;
                }

                if (hoc == -1)
                {
                    hoc = 0;
                    ohle = 0;
                }

                for (i = 0; i < highPassFilter.Length; i++)
                {
                    hi[i] *= -1.0f;
                }
            }

            for (clRw = 0; clRw < len1; clRw++)
            {
                limg = @new + (clRw * pitch);
                himg = limg;
                *himg = 0.0f;
                *(himg + stride) = 0.0f;

                if (invert)
                {
                    hipass = old + (clRw * pitch);
                    lopass = hipass + (stride * hlen);
                }
                else
                {
                    lopass = old + (clRw * pitch);
                    hipass = lopass + (stride * llen);
                }

                lp0 = lopass;
                lp1 = lp0 + ((llen - 1) * stride);
                lspx = lp0 + (loc * stride);
                lspxstr = nstr;
                lstap = lotap;
                lle2 = olle;
                lre2 = olre;

                hp0 = hipass;
                hp1 = hp0 + ((hlen - 1) * stride);
                hspx = hp0 + (hoc * stride);
                hspxstr = nstr;
                hstap = hotap;
                hle2 = ohle;
                hre2 = ohre;
                osfac = ssfac;

                for (var pix = 0; pix < hlen; pix++)
                {
                    for (tap = lstap; tap >= 0; tap--)
                    {
                        lle = lle2;
                        lre = lre2;
                        lpx = lspx;
                        lpxstr = lspxstr;
                        var lowValue = *lpx * lo[tap];

                        for (i = tap + 2; i < lowPassFilter.Length; i += 2)
                        {
                            if (lpx == lp0)
                            {
                                if (lle != 0)
                                {
                                    lpxstr = 0;
                                    lle = 0;
                                }
                                else
                                {
                                    lpxstr = pstr;
                                }
                            }

                            if (lpx == lp1)
                            {
                                if (lre != 0)
                                {
                                    lpxstr = 0;
                                    lre = 0;
                                }
                                else
                                {
                                    lpxstr = nstr;
                                }
                            }

                            lpx += lpxstr;
                            lowValue = MathF.FusedMultiplyAdd(*lpx, lo[i], lowValue);
                        }

                        *limg = lowValue;
                        limg += stride;
                    }

                    if (lspx == lp0)
                    {
                        if (lle2 != 0)
                        {
                            lspxstr = 0;
                            lle2 = 0;
                        }
                        else
                        {
                            lspxstr = pstr;
                        }
                    }

                    lspx += lspxstr;
                    lstap = 1;

                    for (tap = hstap; tap >= 0; tap--)
                    {
                        hle = hle2;
                        hre = hre2;
                        hpx = hspx;
                        hpxstr = hspxstr;
                        fhre = ofhre;
                        sfac = osfac;
                        var highValue = *himg;

                        for (i = tap; i < highPassFilter.Length; i += 2)
                        {
                            if (hpx == hp0)
                            {
                                if (hle != 0)
                                {
                                    hpxstr = 0;
                                    hle = 0;
                                }
                                else
                                {
                                    hpxstr = pstr;
                                    sfac = 1.0f;
                                }
                            }

                            if (hpx == hp1)
                            {
                                if (hre != 0)
                                {
                                    hpxstr = 0;
                                    hre = 0;

                                    if (asym != 0 && daEv != 0)
                                    {
                                        hre = 1;
                                        fhre--;
                                        sfac = fhre;

                                        if (sfac == 0.0f)
                                        {
                                            hre = 0;
                                        }
                                    }
                                }
                                else
                                {
                                    hpxstr = nstr;

                                    if (asym != 0)
                                    {
                                        sfac = -1.0f;
                                    }
                                }
                            }

                            highValue = MathF.FusedMultiplyAdd(*hpx * hi[i], sfac, highValue);
                            hpx += hpxstr;
                        }

                        *himg = highValue;
                        himg += stride;
                    }

                    if (hspx == hp0)
                    {
                        if (hle2 != 0)
                        {
                            hspxstr = 0;
                            hle2 = 0;
                        }
                        else
                        {
                            hspxstr = pstr;
                            osfac = 1.0f;
                        }
                    }

                    hspx += hspxstr;
                    hstap = 1;
                }

                if (daEv != 0)
                {
                    if (lotap != 0)
                    {
                        lstap = 1;
                    }
                    else
                    {
                        lstap = 0;
                    }
                }
                else if (lotap != 0)
                {
                    lstap = 2;
                }
                else
                {
                    lstap = 1;
                }

                for (tap = 1; tap >= lstap; tap--)
                {
                    lle = lle2;
                    lre = lre2;
                    lpx = lspx;
                    lpxstr = lspxstr;
                    var lowValue = *lpx * lo[tap];

                    for (i = tap + 2; i < lowPassFilter.Length; i += 2)
                    {
                        if (lpx == lp0)
                        {
                            if (lle != 0)
                            {
                                lpxstr = 0;
                                lle = 0;
                            }
                            else
                            {
                                lpxstr = pstr;
                            }
                        }

                        if (lpx == lp1)
                        {
                            if (lre != 0)
                            {
                                lpxstr = 0;
                                lre = 0;
                            }
                            else
                            {
                                lpxstr = nstr;
                            }
                        }

                        lpx += lpxstr;
                        lowValue = MathF.FusedMultiplyAdd(*lpx, lo[i], lowValue);
                    }

                    *limg = lowValue;
                    limg += stride;
                }

                if (daEv != 0)
                {
                    if (hotap != 0)
                    {
                        hstap = 1;
                    }
                    else
                    {
                        hstap = 0;
                    }

                    if (highPassFilter.Length == 2)
                    {
                        hspx -= hspxstr;
                        fhre = 1;
                    }
                }
                else if (hotap != 0)
                {
                    hstap = 2;
                }
                else
                {
                    hstap = 1;
                }

                for (tap = 1; tap >= hstap; tap--)
                {
                    hle = hle2;
                    hre = hre2;
                    hpx = hspx;
                    hpxstr = hspxstr;
                    sfac = osfac;
                    var highValue = *himg;

                    if (highPassFilter.Length != 2)
                    {
                        fhre = ofhre;
                    }

                    for (i = tap; i < highPassFilter.Length; i += 2)
                    {
                        if (hpx == hp0)
                        {
                            if (hle != 0)
                            {
                                hpxstr = 0;
                                hle = 0;
                            }
                            else
                            {
                                hpxstr = pstr;
                                sfac = 1.0f;
                            }
                        }

                        if (hpx == hp1)
                        {
                            if (hre != 0)
                            {
                                hpxstr = 0;
                                hre = 0;

                                if (asym != 0 && daEv != 0)
                                {
                                    hre = 1;
                                    fhre--;
                                    sfac = fhre;

                                    if (sfac == 0.0f)
                                    {
                                        hre = 0;
                                    }
                                }
                            }
                            else
                            {
                                hpxstr = nstr;

                                if (asym != 0)
                                {
                                    sfac = -1.0f;
                                }
                            }
                        }

                        highValue = MathF.FusedMultiplyAdd(*hpx * hi[i], sfac, highValue);
                        hpx += hpxstr;
                    }

                    *himg = highValue;
                    himg += stride;
                }
            }

            if (fiEv == 0)
            {
                for (i = 0; i < highPassFilter.Length; i++)
                {
                    hi[i] *= -1.0f;
                }
            }
        }
    }

    private static byte[] ConvertToBytePixels(float[] image, int width, int height, float shift, float scale)
    {
        var pixels = new byte[width * height];
        var imageIndex = 0;

        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var value = MathF.FusedMultiplyAdd(image[imageIndex], scale, shift);
                value += 0.5f;

                pixels[imageIndex] = value switch
                {
                    < 0.0f => 0,
                    > 255.0f => 255,
                    _ => (byte)value,
                };

                imageIndex++;
            }
        }

        return pixels;
    }
}
