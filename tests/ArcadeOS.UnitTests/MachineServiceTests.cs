using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Application.Services;
using ArcadeOS.Api.Domain.Entities;
using ArcadeOS.Api.Domain.Enums;
using ArcadeOS.Api.Domain.Exceptions;
using ArcadeOS.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ArcadeOS.UnitTests;

public class MachineServiceTests
{
    private ArcadeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ArcadeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ArcadeDbContext(options);
    }

    private readonly Mock<ILogger<MachineService>> _mockLogger = new();

    [Fact]
    public async Task ProcessHeartbeatAsync_UpdatesMachineStatus_And_CreatesHeartbeatRecord()
    {
        // Arrange
        using var db = CreateDbContext();
        var machine = new Machine { Id = Guid.NewGuid(), Name = "Test Machine", Status = MachineStatus.Offline };
        db.Machines.Add(machine);
        await db.SaveChangesAsync();

        var service = new MachineService(db, _mockLogger.Object);
        var dto = new MachineHeartbeatDto(machine.Id, MachineStatus.Online, 100m, 50, null, null);

        // Act
        await service.ProcessHeartbeatAsync(dto);

        // Assert
        var updatedMachine = await db.Machines.FindAsync(machine.Id);
        Assert.Equal(MachineStatus.Online, updatedMachine!.Status);

        var heartbeats = await db.MachineHeartbeats.ToListAsync();
        Assert.Single(heartbeats);
        Assert.Equal(MachineStatus.Online, heartbeats[0].Status);
    }

    [Fact]
    public async Task ProcessHeartbeatAsync_InvalidMachineId_ThrowsNotFoundException()
    {
        using var db = CreateDbContext();
        var service = new MachineService(db, _mockLogger.Object);
        
        var dto = new MachineHeartbeatDto(Guid.NewGuid(), MachineStatus.Online, 10m, 5, null, null);

        await Assert.ThrowsAsync<NotFoundException>(() => service.ProcessHeartbeatAsync(dto));
    }

    [Fact]
    public async Task MarkStaleMachinesOfflineAsync_MarksOnlyStaleMachines()
    {
        // Arrange
        using var db = CreateDbContext();
        
        var machine1 = new Machine { Id = Guid.NewGuid(), Name = "Stale Machine", Status = MachineStatus.Online };
        var machine2 = new Machine { Id = Guid.NewGuid(), Name = "Fresh Machine", Status = MachineStatus.Online };
        
        db.Machines.AddRange(machine1, machine2);

        // machine1 sent a heartbeat 10 minutes ago
        db.MachineHeartbeats.Add(new MachineHeartbeat 
        { 
            MachineId = machine1.Id, Status = MachineStatus.Online, ReceivedAt = DateTime.UtcNow.AddMinutes(-10) 
        });

        // machine2 sent a heartbeat 1 minute ago
        db.MachineHeartbeats.Add(new MachineHeartbeat 
        { 
            MachineId = machine2.Id, Status = MachineStatus.Online, ReceivedAt = DateTime.UtcNow.AddMinutes(-1) 
        });

        await db.SaveChangesAsync();

        var service = new MachineService(db, _mockLogger.Object);

        // Act: mark machines offline if no heartbeat in last 5 minutes
        await service.MarkStaleMachinesOfflineAsync(TimeSpan.FromMinutes(5));

        // Assert
        var updatedM1 = await db.Machines.FindAsync(machine1.Id);
        var updatedM2 = await db.Machines.FindAsync(machine2.Id);

        Assert.Equal(MachineStatus.Offline, updatedM1!.Status);
        Assert.Equal(MachineStatus.Online, updatedM2!.Status);
    }
}
