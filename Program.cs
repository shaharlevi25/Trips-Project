using Microsoft.AspNetCore.Authentication.Cookies;
using TripsProject.Data;
using TripsProject.Services;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<PackageRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<DiscountRepository>();
builder.Services.AddScoped<BookingRulesRepository>();
builder.Services.AddScoped<PolicyTextService>();
builder.Services.AddScoped<WaitingListRepository>();
builder.Services.AddScoped<BookingRepository>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<OrderRepository>();
builder.Services.AddScoped<OrderCleanupService>();
builder.Services.AddScoped<DiscountService>();





builder.Services.AddControllersWithViews();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/User/Login";
        options.LogoutPath = "/User/Logout";
        options.AccessDeniedPath = "/User/Login";
    });
builder.Services.AddSession();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

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
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();


app.MapControllerRoute(
        name: "default",
        pattern: "{controller=HomePage}/{action=HomePage}/{id?}")
    .WithStaticAssets();


app.Run();