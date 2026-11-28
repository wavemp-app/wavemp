namespace WaveMP.Core.Analysis.Features;

/// <summary>
/// An in-place radix-2 fast Fourier transform for lengths that are a power of two.
/// </summary>
internal static class Fft
{
    /// <summary>
    /// Transforms the complex values (real and imaginary parts of the same length) in place.
    /// </summary>
    public static void Transform(Span<float> real, Span<float> imaginary)
    {
        int length = real.Length;
        if (length != imaginary.Length || length == 0 || (length & (length - 1)) != 0)
        {
            throw new ArgumentException("The length must be the same power of two for both parts.", nameof(real));
        }

        // Bit-reversed order.
        for (int index = 1, reversed = 0; index < length; index++)
        {
            int bit = length >> 1;
            for (; (reversed & bit) != 0; bit >>= 1)
            {
                reversed ^= bit;
            }

            reversed ^= bit;
            if (index < reversed)
            {
                (real[index], real[reversed]) = (real[reversed], real[index]);
                (imaginary[index], imaginary[reversed]) = (imaginary[reversed], imaginary[index]);
            }
        }

        for (int size = 2; size <= length; size <<= 1)
        {
            double angle = -2 * Math.PI / size;
            float stepReal = (float)Math.Cos(angle);
            float stepImaginary = (float)Math.Sin(angle);
            for (int start = 0; start < length; start += size)
            {
                float twiddleReal = 1;
                float twiddleImaginary = 0;
                for (int offset = 0; offset < size / 2; offset++)
                {
                    int even = start + offset;
                    int odd = even + (size / 2);
                    float oddReal = (real[odd] * twiddleReal) - (imaginary[odd] * twiddleImaginary);
                    float oddImaginary = (real[odd] * twiddleImaginary) + (imaginary[odd] * twiddleReal);
                    real[odd] = real[even] - oddReal;
                    imaginary[odd] = imaginary[even] - oddImaginary;
                    real[even] += oddReal;
                    imaginary[even] += oddImaginary;
                    float nextReal = (twiddleReal * stepReal) - (twiddleImaginary * stepImaginary);
                    twiddleImaginary = (twiddleReal * stepImaginary) + (twiddleImaginary * stepReal);
                    twiddleReal = nextReal;
                }
            }
        }
    }
}
