using FluentResults;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telemetry.Domain.Entities;

namespace Telemetry.Domain.Repositories;

public interface ISidecarRepository
{
    Task<Sidecar> LoadAsync(CancellationToken ct);
    Task<Result> SaveAsync(Sidecar state, CancellationToken ct);
}