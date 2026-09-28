using ByeMoney.API.Contracts.Courses;
using ByeMoney.API.Controllers;
using ByeMoney.Application.Modules.Identity.Users.Commands.SyncUserFromStrapi;
using ByeMoney.Application.Modules.Purchases.Commands.AdminPurchaseCourses;
using ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourses;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Identity.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ByeMoney.UnitTests;

public class AdminPurchaseCoursesTests
{
    [Fact]
    public async Task Handler_UsesBeneficiaryIdentityAndExistingPurchaseCommand()
    {
        var beneficiaryId = Guid.NewGuid();
        var courses = new[] { "course-1", "course-2" };
        var expected = new PurchaseCoursesResponse(Guid.NewGuid(), 250m, [], DateTime.UtcNow);
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.Is<SyncUserFromStrapiCommand>(c =>
                c.ExternalUserId == "beneficiary-document-id"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(beneficiaryId);
        sender.Setup(x => x.Send(It.Is<PurchaseCoursesCommand>(c =>
                c.BuyerUserId == beneficiaryId && c.ExternalCourseIds == courses &&
                c.IsFree && c.FreeReason == "هدیه ادمین"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PurchaseCoursesResponse>.Success(expected));

        var handler = new AdminPurchaseCoursesCommandHandler(sender.Object);
        var result = await handler.Handle(
            new AdminPurchaseCoursesCommand("beneficiary-document-id", courses, true, "هدیه ادمین"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(expected);
        sender.Verify(x => x.Send(It.IsAny<PurchaseCoursesCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Controller_ReturnsOkWithTransactionIdAndZeroPriceForComplimentaryPurchase()
    {
        var purchaseId = Guid.NewGuid();
        var response = new PurchaseCoursesResponse(
            Guid.NewGuid(), 0m,
            [new PurchasedCourseItemResponse(purchaseId, "course-1", "دوره", 0m, "Debited")],
            DateTime.UtcNow);
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<AdminPurchaseCoursesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PurchaseCoursesResponse>.Success(response));

        var controller = new AdminCoursesController(sender.Object);
        var result = await controller.PurchaseCourses(
            new AdminPurchaseCoursesApiRequest("beneficiary", ["course-1"], true, "هدیه ادمین"), CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var body = ok.Value
            .Should().BeOfType<PurchaseCoursesApiResponse>().Subject;
        body.TransactionId.Should().Be(response.TransactionId);
        body.TransactionId.Should().NotBeEmpty();
        body.TotalPriceInNoor.Should().Be(0m);
        body.Items.Should().ContainSingle().Which.PurchaseId.Should().Be(purchaseId);
        sender.Verify(x => x.Send(It.Is<AdminPurchaseCoursesCommand>(c =>
            c.BeneficiaryExternalUserId == "beneficiary" &&
            c.ExternalCourseIds.SequenceEqual(new[] { "course-1" }) &&
            c.IsFree && c.FreeReason == "هدیه ادمین"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Controller_RequiresAdminRoleOnExpectedRoute()
    {
        var type = typeof(AdminCoursesController);
        type.GetCustomAttributes(typeof(RouteAttribute), true)
            .Cast<RouteAttribute>().Single().Template.Should().Be("api/admin/courses");
        type.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single().Roles.Should().Be(RoleNames.Admin);
        typeof(AdminCoursesController).GetMethod(nameof(AdminCoursesController.PurchaseCourses))!
            .GetCustomAttributes(typeof(HttpPostAttribute), true)
            .Cast<HttpPostAttribute>().Single().Template.Should().Be("purchase");
    }

    [Fact]
    public async Task Validator_RejectsInvalidBeneficiaryBeforePurchase()
    {
        var validator = new AdminPurchaseCoursesCommandValidator();
        var result = await validator.ValidateAsync(
            new AdminPurchaseCoursesCommand("", ["course-1"]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "BeneficiaryExternalUserId");
    }
}
