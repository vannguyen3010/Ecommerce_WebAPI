var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();


//Runtime
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages().AddRazorRuntimeCompilation();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}


app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    #region Home
    endpoints.MapControllerRoute(
       name: "default",
       pattern: "{controller=Home}/{action=Index}/{id?}");
    #endregion

    #region Product
    endpoints.MapControllerRoute(
        name: "product",
        pattern: "san-pham",
        defaults: new { controller = "Products", action = "Index" });
    #endregion

    #region Introduce
    endpoints.MapControllerRoute(
       name: "introduce",
       pattern: "gioi-thieu",
       defaults: new { controller = "Introduce", action = "Index" });
    #endregion

    #region Contact
    endpoints.MapControllerRoute(
       name: "contact",
       pattern: "lien-he",
       defaults: new { controller = "Contact", action = "Index" });
    #endregion
});

app.Run();
