namespace ShortcutManager.Windows.Tests;

internal static class LaunchProbeCleanup
{
    internal static async Task RemoveAsync(string ownedFixtureDirectory)
    {
        const int maxAttempts = 16;
        const int sharingViolation = unchecked((int)0x80070020);
        const int lockViolation = unchecked((int)0x80070021);

        // Windows may still be accessing the test executable after its process
        // exits. Retry only these filesystem errors in the test's own folder.
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Delete(ownedFixtureDirectory, recursive: true);
                return;
            }
            catch (Exception error) when (error is UnauthorizedAccessException ||
                error is IOException && error.HResult is sharingViolation or lockViolation)
            {
                if (attempt == maxAttempts)
                    throw new IOException($"Could not remove the isolated launch fixture after {attempt} attempts: {ownedFixtureDirectory}", error);
                await Task.Delay(200);
            }
        }
    }
}
