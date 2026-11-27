using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// Solves the challenge LRCLIB asks for before it takes published lyrics (a proof of work): a number (nonce) whose
/// SHA-256 hash, together with the prefix of the challenge (<c>SHA256(prefix + nonce)</c>), is at most the target as
/// bytes. The search runs on several threads; any valid nonce is accepted.
/// </summary>
public static class LrclibChallenge
{
    /// <summary>
    /// Nonces tried between two looks at the cancellation.
    /// </summary>
    private const int CheckInterval = 1 << 14;

    /// <summary>
    /// The first valid nonce one of the threads finds.
    /// </summary>
    /// <param name="prefix">The prefix of the challenge.</param>
    /// <param name="target">The target of the challenge as hexadecimal text.</param>
    /// <param name="cancellationToken">Stops the search.</param>
    /// <exception cref="FormatException">The target is no hexadecimal text.</exception>
    /// <exception cref="OperationCanceledException">The search was stopped.</exception>
    public static long Solve(string prefix, string target, CancellationToken cancellationToken = default)
    {
        byte[] targetBytes = Convert.FromHexString(target);
        byte[] prefixBytes = Encoding.UTF8.GetBytes(prefix);
        int workers = Math.Max(1, Environment.ProcessorCount);
        long found = -1;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            Parallel.For(
                0,
                workers,
                new ParallelOptions { CancellationToken = stop.Token, MaxDegreeOfParallelism = workers },
                worker =>
                {
                    if (Search(prefixBytes, targetBytes, worker, workers, stop.Token) is { } nonce)
                    {
                        Interlocked.CompareExchange(ref found, nonce, -1);
                        stop.Cancel();
                    }
                });
        }
        catch (OperationCanceledException) when (Interlocked.Read(ref found) >= 0)
        {
            // Another thread found the nonce.
        }

        long result = Interlocked.Read(ref found);
        cancellationToken.ThrowIfCancellationRequested();
        return result >= 0 ? result : throw new OperationCanceledException(cancellationToken);
    }

    /// <summary>
    /// Whether the nonce solves the challenge.
    /// </summary>
    public static bool IsSolution(string prefix, string target, long nonce)
    {
        byte[] hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(prefix + nonce.ToString(CultureInfo.InvariantCulture)));
        return hash.AsSpan().SequenceCompareTo(Convert.FromHexString(target)) <= 0;
    }

    /// <summary>
    /// Tries the nonces <c>start</c>, <c>start + step</c> and so on until one solves the challenge; <c>null</c> once
    /// the search is stopped.
    /// </summary>
    private static long? Search(byte[] prefix, byte[] target, long start, long step, CancellationToken token)
    {
        Span<byte> buffer = stackalloc byte[prefix.Length + 20];
        prefix.CopyTo(buffer);
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        int tries = 0;
        for (long nonce = start; nonce >= 0; nonce += step)
        {
            if (++tries == CheckInterval)
            {
                tries = 0;
                if (token.IsCancellationRequested)
                {
                    return null;
                }
            }

            nonce.TryFormat(buffer[prefix.Length..], out int written, default, CultureInfo.InvariantCulture);
            SHA256.HashData(buffer[..(prefix.Length + written)], hash);
            if (hash.SequenceCompareTo(target) <= 0)
            {
                return nonce;
            }
        }

        return null;
    }
}
