using Xunit;

// Avalonia headless admite una sola sesión a la vez; las clases con HeadlessUnitTestSession
// no pueden correr en paralelo. La suite completa dura ~2 s, así que no compensa paralelizar.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
