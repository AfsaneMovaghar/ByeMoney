using ByeMoney.Application;
using ByeMoney.Infrastructure.Persistence;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ByeMoney.UnitTests;

public sealed class ApplicationStructureTests
{
    [Fact]
    public void EachRequestHasExactlyOneDedicatedHandlerRegistration()
    {
        var services = new ServiceCollection().AddApplicationServices();
        var requests = typeof(ByeMoney.Application.DependencyInjection).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface)
            .SelectMany(type => type.GetInterfaces()
                .Where(contract => contract.IsGenericType &&
                    contract.GetGenericTypeDefinition() == typeof(IRequest<>))
                .Select(contract => new
                {
                    Request = type,
                    Response = contract.GetGenericArguments()[0]
                }))
            .ToArray();

        requests.Should().NotBeEmpty();

        foreach (var request in requests)
        {
            var contract = typeof(IRequestHandler<,>).MakeGenericType(request.Request, request.Response);
            var registration = services.Where(service => service.ServiceType == contract)
                .Should().ContainSingle(request.Request.FullName).Subject;

            registration.ImplementationType.Should().NotBeNull();
            registration.ImplementationType!.GetInterfaces()
                .Count(type => type.IsGenericType &&
                    type.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                .Should().Be(1, request.Request.FullName);
        }
    }

    [Fact]
    public void EachCommandRetainsExactlyOneValidatorRegistration()
    {
        var services = new ServiceCollection().AddApplicationServices();
        var commands = typeof(ByeMoney.Application.DependencyInjection).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && type.Name.EndsWith("Command", StringComparison.Ordinal) &&
                type.GetInterfaces().Any(contract => contract.IsGenericType &&
                    contract.GetGenericTypeDefinition() == typeof(IRequest<>)))
            .ToArray();

        commands.Should().NotBeEmpty();

        foreach (var command in commands)
        {
            var contract = typeof(IValidator<>).MakeGenericType(command);
            services.Where(service => service.ServiceType == contract)
                .Should().ContainSingle(command.FullName);
        }
    }

    [Fact]
    public void PersistenceModelStillMatchesTheMigrationSnapshot()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql()
            .Options;
        using var context = new ApplicationDbContext(options);

        context.Database.HasPendingModelChanges().Should().BeFalse();
    }
}
