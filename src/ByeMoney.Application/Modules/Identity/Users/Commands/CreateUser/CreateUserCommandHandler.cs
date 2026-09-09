using MediatR;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;

namespace ByeMoney.Application.Modules.Identity.Users.Commands.CreateUser;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, UserId>
{
    private readonly IRepository<User, UserId> _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateUserCommandHandler(
        IRepository<User, UserId> userRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<UserId> Handle(CreateUserCommand request, CancellationToken ct)
    {
        var user = User.CreateFromStrapi(
            request.ExternalUserId,
            request.Phone,
            request.Email,
            request.FirstName,
            request.LastName,
            request.Confirmed,
            request.Blocked,
            request.UserType);

        await _userRepository.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);   

        return user.Id;
    }
}