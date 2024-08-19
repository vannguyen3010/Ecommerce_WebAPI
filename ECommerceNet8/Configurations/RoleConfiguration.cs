using ECommerceNet8.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceNet8.Configurations
{
    public class RoleConfiguration : IEntityTypeConfiguration<IdentityRole>
    {
        public void Configure(EntityTypeBuilder<IdentityRole> builder)
        {
            builder.HasData(
                new IdentityRole
                {
                    //Id = "1372a768-421a-440d-ac9b-3297151b4fd7",
                    Id = "6a61c1da-5dfb-4830-a9b6-951a90e835e2",
                    Name = Roles.Administrator,
                    NormalizedName = Roles.Administrator.ToUpper()
                },

                new IdentityRole
                {
                    //Id = "a0ee1d85-8b89-4ce2-8736-8e8476747129",
                    Id = "76ca7709-58b2-4da1-bb5f-53d5464eb0bb",
                    Name = Roles.Customer,
                    NormalizedName = Roles.Customer.ToUpper()
                });
        }
    }
}
