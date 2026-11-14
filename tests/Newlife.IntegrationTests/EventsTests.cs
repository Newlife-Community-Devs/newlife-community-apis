using System.Net;
using System.Net.Http.Json;
using Newlife.IntegrationTests.Infrastructure;
using Shouldly;

namespace Newlife.IntegrationTests;

public sealed class EventsTests(NoRateLimitApiFactory factory) : IClassFixture<NoRateLimitApiFactory>
{
    [Fact]
    public async Task Create_update_publish_and_public_reads()
    {
        var client = await AdminClientAsync();
        var title = $"Concert {Guid.NewGuid():N}";
        var startAt = DateTime.UtcNow.AddDays(20);

        var created = await client.PostAsJsonAsync("/api/v1/admin/events", new
        {
            title,
            description = "An evening of worship.",
            venue = "Main Auditorium",
            startAt,
            endAt = startAt.AddHours(3),
            isPublished = false,
            isFeatured = true,
            ticketingEnabled = false
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var evt = await created.Content.ReadFromJsonAsync<EventResponse>();
        evt.ShouldNotBeNull();
        evt.Slug.ShouldBe("concert-" + title.Split(' ')[1]);
        evt.IsPublished.ShouldBeFalse();

        var hidden = await client.GetAsync($"/api/v1/events/{evt.Slug}");
        hidden.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var publish = await client.PostAsJsonAsync($"/api/v1/admin/events/{evt.Id}/publish", new { isPublished = true });
        publish.StatusCode.ShouldBe(HttpStatusCode.OK);

        var publicGet = await factory.CreateClient().GetFromJsonAsync<EventResponse>($"/api/v1/events/{evt.Slug}");
        publicGet.ShouldNotBeNull();
        publicGet.Title.ShouldBe(title);

        var newTitle = $"Updated Concert {Guid.NewGuid():N}";
        var update = await client.PutAsJsonAsync($"/api/v1/admin/events/{evt.Id}", new
        {
            title = newTitle,
            description = (string?)null,
            venue = "New Venue",
            startAt,
            endAt = (DateTime?)null,
            bannerMediaId = (Guid?)null,
            isPublished = true,
            isFeatured = false,
            ticketingEnabled = false
        });
        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<EventResponse>();
        updated.ShouldNotBeNull();
        updated.Slug.ShouldBe(evt.Slug);
        updated.Title.ShouldBe(newTitle);

        var delete = await client.DeleteAsync($"/api/v1/admin/events/{evt.Id}");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var gone = await factory.CreateClient().GetAsync($"/api/v1/events/{evt.Slug}");
        gone.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Slug_rules_are_enforced()
    {
        var client = await AdminClientAsync();
        var startAt = DateTime.UtcNow.AddDays(5);

        var conflict = await client.PostAsJsonAsync("/api/v1/admin/events", new
        {
            title = "Duplicate slug event",
            slug = "youth-camp-2026",
            startAt,
            isPublished = false
        });
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await AuthTestHelpers.ReadProblemCodeAsync(conflict)).ShouldBe("event.slug_taken");

        var invalid = await client.PostAsJsonAsync("/api/v1/admin/events", new
        {
            title = "Bad slug event",
            slug = "Bad Slug!",
            startAt,
            isPublished = false
        });
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AuthTestHelpers.ReadProblemCodeAsync(invalid)).ShouldBe("event.invalid_slug");

        var badDates = await client.PostAsJsonAsync("/api/v1/admin/events", new
        {
            title = "Bad dates event",
            startAt,
            endAt = startAt.AddDays(-1),
            isPublished = false
        });
        badDates.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AuthTestHelpers.ReadProblemCodeAsync(badDates)).ShouldBe("validation.failed");
    }

    [Fact]
    public async Task Ticket_types_and_public_sale_calculation()
    {
        var client = await AdminClientAsync();
        var slug = $"tickets-{Guid.NewGuid():N}";
        var startAt = DateTime.UtcNow.AddDays(15);

        var created = await client.PostAsJsonAsync("/api/v1/admin/events", new
        {
            title = "Ticketed event",
            slug,
            startAt,
            isPublished = true,
            ticketingEnabled = true
        });
        created.EnsureSuccessStatusCode();
        var evt = await created.Content.ReadFromJsonAsync<EventResponse>();
        evt.ShouldNotBeNull();

        var regular = await client.PostAsJsonAsync($"/api/v1/admin/events/{evt.Id}/ticket-types", new
        {
            name = "Regular",
            description = (string?)null,
            price = 50.00,
            quantity = 100,
            maxPerOrder = 10,
            salesStartAt = (DateTime?)null,
            salesEndAt = (DateTime?)null,
            isActive = true
        });
        regular.StatusCode.ShouldBe(HttpStatusCode.Created);
        var regularType = await regular.Content.ReadFromJsonAsync<TicketTypeResponse>();
        regularType.ShouldNotBeNull();

        var badQuantity = await client.PostAsJsonAsync($"/api/v1/admin/events/{evt.Id}/ticket-types", new
        {
            name = "Bad",
            price = 10.00,
            quantity = 0,
            maxPerOrder = 10,
            isActive = true
        });
        badQuantity.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var badMaxPerOrder = await client.PostAsJsonAsync($"/api/v1/admin/events/{evt.Id}/ticket-types", new
        {
            name = "Bad Max",
            price = 10.00,
            quantity = 3,
            maxPerOrder = 10,
            isActive = true
        });
        badMaxPerOrder.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var upcomingPublic = await factory.CreateClient()
            .GetFromJsonAsync<TicketTypeResponse[]>($"/api/v1/events/{slug}/ticket-types");
        upcomingPublic.ShouldNotBeNull();
        var publicRegular = upcomingPublic.Single(type => type.Name == "Regular");
        publicRegular.Price.ShouldBe(50.00m);
        publicRegular.Remaining.ShouldBe(100);
        publicRegular.IsOnSale.ShouldBeTrue();

        var futureStart = await client.PostAsJsonAsync($"/api/v1/admin/events/{evt.Id}/ticket-types", new
        {
            name = "Early Bird",
            price = 30.00,
            quantity = 50,
            maxPerOrder = 2,
            salesStartAt = DateTime.UtcNow.AddDays(3),
            salesEndAt = (DateTime?)null,
            isActive = true
        });
        futureStart.EnsureSuccessStatusCode();

        var afterAdd = await factory.CreateClient()
            .GetFromJsonAsync<TicketTypeResponse[]>($"/api/v1/events/{slug}/ticket-types");
        afterAdd.ShouldNotBeNull();
        afterAdd.Single(type => type.Name == "Early Bird").IsOnSale.ShouldBeFalse();

        var update = await client.PutAsJsonAsync($"/api/v1/admin/ticket-types/{regularType.Id}", new
        {
            name = "Regular",
            price = 60.00,
            quantity = 120,
            maxPerOrder = 10,
            isActive = true
        });
        update.StatusCode.ShouldBe(HttpStatusCode.OK);

        var remove = await client.DeleteAsync($"/api/v1/admin/ticket-types/{regularType.Id}");
        remove.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterRemove = await factory.CreateClient()
            .GetFromJsonAsync<TicketTypeResponse[]>($"/api/v1/events/{slug}/ticket-types");
        afterRemove.ShouldNotBeNull();
        afterRemove.ShouldNotContain(type => type.Name == "Regular");
    }

    [Fact]
    public async Task Public_ticket_types_require_enabled_and_published_event()
    {
        var client = await AdminClientAsync();
        var slug = $"no-tickets-{Guid.NewGuid():N}";

        var created = await client.PostAsJsonAsync("/api/v1/admin/events", new
        {
            title = "No ticketing event",
            slug,
            startAt = DateTime.UtcNow.AddDays(7),
            isPublished = true,
            ticketingEnabled = false
        });
        created.EnsureSuccessStatusCode();

        var disabled = await factory.CreateClient().GetAsync($"/api/v1/events/{slug}/ticket-types");
        disabled.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await AuthTestHelpers.ReadProblemCodeAsync(disabled)).ShouldBe("ticketing.disabled");

        var unknown = await factory.CreateClient().GetAsync($"/api/v1/events/does-not-exist-{Guid.NewGuid():N}/ticket-types");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await AuthTestHelpers.ReadProblemCodeAsync(unknown)).ShouldBe("event.not_found");
    }

    [Fact]
    public async Task EventManager_can_manage_ticket_types_but_not_events()
    {
        var client = await AdminClientAsync();
        var email = $"manager-{Guid.NewGuid():N}@newlife.test";

        var create = await client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            fullName = "Event Manager",
            password = "ManagerPass123",
            roles = new[] { "EventManager" }
        });
        create.EnsureSuccessStatusCode();

        var managerClient = factory.CreateClient();
        var manager = await AuthTestHelpers.LoginAsync(managerClient, email, "ManagerPass123");
        managerClient.WithBearer(manager.AccessToken);

        var forbidden = await managerClient.PostAsJsonAsync("/api/v1/admin/events", new
        {
            title = "Should fail",
            startAt = DateTime.UtcNow.AddDays(2),
            isPublished = false
        });
        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var startAt = DateTime.UtcNow.AddDays(9);
        var evt = await client.PostAsJsonAsync("/api/v1/admin/events", new
        {
            title = "Manager ticketing event",
            startAt,
            isPublished = true,
            ticketingEnabled = true
        });
        evt.EnsureSuccessStatusCode();
        var evtBody = await evt.Content.ReadFromJsonAsync<EventResponse>();
        evtBody.ShouldNotBeNull();

        var ticket = await managerClient.PostAsJsonAsync(
            $"/api/v1/admin/events/{evtBody.Id}/ticket-types",
            new
            {
                name = "Standard",
                price = 20.00,
                quantity = 50,
                maxPerOrder = 5,
                isActive = true
            });
        ticket.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        var tokens = await AuthTestHelpers.LoginAsync(
            client, AuthTestHelpers.AdminEmail, AuthTestHelpers.AdminPassword);
        return client.WithBearer(tokens.AccessToken);
    }

    private sealed record EventResponse(
        Guid Id,
        string Slug,
        string Title,
        string? Description,
        string? Venue,
        DateTime StartAt,
        DateTime? EndAt,
        Guid? BannerMediaId,
        string? BannerUrl,
        bool IsPublished,
        bool IsFeatured,
        bool TicketingEnabled,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        TicketTypeResponse[] TicketTypes);

    private sealed record TicketTypeResponse(
        Guid Id,
        Guid EventId,
        string Name,
        string? Description,
        decimal Price,
        string Currency,
        int Quantity,
        int MaxPerOrder,
        DateTime? SalesStartAt,
        DateTime? SalesEndAt,
        bool IsActive,
        int Remaining,
        bool IsOnSale);
}
