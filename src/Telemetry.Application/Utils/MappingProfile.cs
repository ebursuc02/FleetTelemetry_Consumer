using AutoMapper;
using Telemetry.Application.DTOs;
using Telemetry.Domain.Entities;

namespace Telemetry.Application.Utils;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Record, RecordDto>().ReverseMap();
    }
}