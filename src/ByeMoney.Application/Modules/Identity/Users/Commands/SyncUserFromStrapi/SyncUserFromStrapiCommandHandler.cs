using MediatR;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Application.Modules.Identity.Users.Interface;

namespace ByeMoney.Application.Modules.Identity.Users.Commands.SyncUserFromStrapi;

public class SyncUserFromStrapiCommandHandler : IRequestHandler<SyncUserFromStrapiCommand, Guid>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SyncUserFromStrapiCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(SyncUserFromStrapiCommand request, CancellationToken ct)
    {
        var user = await _userRepository.GetByExternalUserIdAsync(request.ExternalUserId, ct);

        if (user is not null)
        {
            user.MarkProfileSynced();
            _userRepository.Update(user);
            await _unitOfWork.SaveChangesAsync(ct);
            return user.Id.Value;
        }

        var newUser = User.CreateFromStrapi(request.ExternalUserId);
        await _userRepository.AddAsync(newUser, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return newUser.Id.Value;
    }
}
