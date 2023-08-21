namespace DotNet.Testcontainers.Tests.Fixtures
{
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Threading.Tasks;
  using DotNet.Testcontainers.Builders;
  using DotNet.Testcontainers.Configurations;
  using DotNet.Testcontainers.Containers;
  using DotNet.Testcontainers.Images;
  using Xunit;

  public abstract class ProtectDockerDaemonSocket : IAsyncLifetime
  {
    private const string CertsDirectoryName = "certs";

    private const ushort TlsPort = 2376;

    private readonly string _containerCertsDirectoryPath = Path.Combine("/", CertsDirectoryName);

    private readonly IContainer _container;

    protected readonly string _hostCertsDirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("D"), CertsDirectoryName);

    protected ProtectDockerDaemonSocket(ContainerBuilder containerConfiguration, string dockerImageVersion)
    {
      _container = containerConfiguration
        .WithImage(DockerImage(dockerImageVersion))
        .WithPrivileged(true)
        .WithPortBinding(TlsPort, true)
        .WithBindMount(_hostCertsDirectoryPath, _containerCertsDirectoryPath, AccessMode.ReadWrite)
        .WithWaitStrategy(Wait.ForUnixContainer().AddCustomWaitStrategy(new UntilListenOn()))
        .Build();
    }

    public virtual IList<string> CustomProperties
    {
      get
      {
        var customProperties = new List<string>();
        customProperties.Add($"docker.host={TcpEndpoint}");
        customProperties.Add($"docker.cert.path={Path.Combine(_hostCertsDirectoryPath, "client")}");
        return customProperties;
      }
    }

    private static IImage DockerImage(string dockerImageVersion)
    {
      return new DockerImage(string.Empty, "docker", dockerImageVersion + "-dind");
    }

    private Uri TcpEndpoint
    {
      get
      {
        return new UriBuilder("tcp", _container.Hostname, _container.GetMappedPublicPort(TlsPort)).Uri;
      }
    }

    public Task InitializeAsync()
    {
      _ = Directory.CreateDirectory(_hostCertsDirectoryPath);
      return _container.StartAsync();
    }

    public Task DisposeAsync()
    {
      return _container.DisposeAsync().AsTask();
    }

    private sealed class UntilListenOn : IWaitUntil
    {
      public async Task<bool> UntilAsync(IContainer container)
      {
        var (_, stderr) = await container.GetLogsAsync()
          .ConfigureAwait(false);

        return stderr != null && stderr.Contains("API listen on [::]:2376");
      }
    }
  }
}
