using Xunit;

// VelumProductRegistryProblemCache и VelumSolidEnvironmentGate — статические
// синглтоны продакшн-кода. Параллельный запуск классов тестов по ним гонял бы
// данные, поэтому параллелизация сборки тестов отключена целиком.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
