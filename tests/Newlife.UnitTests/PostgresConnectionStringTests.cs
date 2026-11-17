using Newlife.Infrastructure.Persistence;
using Shouldly;

namespace Newlife.UnitTests;

public sealed class PostgresConnectionStringTests
{
    [Fact]
    public void Neon_uri_is_converted_to_npgsql_key_value_form()
    {
        const string uri = "postgresql://neondb_owner:npg_secret@ep-demo-pooler.c-2.eu-west-2.aws.neon.tech/neondb?channel_binding=require&sslmode=require";

        var result = PostgresConnectionString.Normalize(uri);

        result.ShouldContain("Host=ep-demo-pooler.c-2.eu-west-2.aws.neon.tech;");
        result.ShouldContain("Port=5432;");
        result.ShouldContain("Username=neondb_owner;");
        result.ShouldContain("Password=npg_secret;");
        result.ShouldContain("Database=neondb;");
        result.ShouldContain("SSL Mode=require;");
        result.ShouldContain("Channel Binding=require;");
    }

    [Fact]
    public void Uri_with_port_and_encoded_password_is_decoded_and_quoted()
    {
        const string uri = "postgresql://app:user%40p%3Bss@db.example.com:6432/proddb?sslmode=require";

        var result = PostgresConnectionString.Normalize(uri);

        result.ShouldContain("Port=6432;");
        result.ShouldContain("""Password="user@p;ss";""");
    }

    [Fact]
    public void Key_value_strings_pass_through_unchanged()
    {
        const string keyValues = "Host=localhost;Port=5433;Database=newlife;Username=newlife;Password=newlife_dev";

        PostgresConnectionString.Normalize(keyValues).ShouldBe(keyValues);
    }
}
