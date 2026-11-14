using Newlife.Application.Prayers.DTOs;
using Newlife.Application.Prayers.Validators;
using Shouldly;

namespace Newlife.UnitTests;

public sealed class PrayerValidatorTests
{
    private readonly SubmitPrayerRequestValidator _validator = new();

    [Fact]
    public void Valid_non_anonymous_request_passes()
    {
        var result = _validator.Validate(new SubmitPrayerRequest(
            "Healing",
            "Please pray for my mother's recovery.",
            "Ama",
            "233244000000",
            false));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Valid_anonymous_request_passes_without_submitter_name()
    {
        var result = _validator.Validate(new SubmitPrayerRequest(
            null,
            "Please pray for my family this week.",
            null,
            null,
            true));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("hi")]
    public void Content_shorter_than_five_characters_fails(string content)
    {
        var result = _validator.Validate(new SubmitPrayerRequest(
            null,
            content,
            "Ama",
            null,
            false));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.PropertyName == nameof(SubmitPrayerRequest.Content));
    }

    [Fact]
    public void Non_anonymous_request_requires_submitter_name()
    {
        var result = _validator.Validate(new SubmitPrayerRequest(
            null,
            "Please pray for my family this week.",
            null,
            null,
            false));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.PropertyName == nameof(SubmitPrayerRequest.SubmitterName));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("0244000000")]
    [InlineData("23324400000")]
    public void Invalid_phone_number_fails(string phoneNumber)
    {
        var result = _validator.Validate(new SubmitPrayerRequest(
            null,
            "Please pray for my family this week.",
            "Ama",
            phoneNumber,
            false));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.PropertyName == nameof(SubmitPrayerRequest.PhoneNumber));
    }

    [Fact]
    public void Title_longer_than_two_hundred_characters_fails()
    {
        var result = _validator.Validate(new SubmitPrayerRequest(
            new string('a', 201),
            "Please pray for my family this week.",
            "Ama",
            null,
            false));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.PropertyName == nameof(SubmitPrayerRequest.Title));
    }
}
