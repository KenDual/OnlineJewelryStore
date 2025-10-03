# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is an ASP.NET MVC 5 web application targeting .NET Framework 4.7.2. It's an Online Jewelry Store project using the traditional ASP.NET MVC pattern with Razor views.

**Architecture**: Classic ASP.NET MVC 5 with:
- Controllers in `OnlineJewelryStore/Controllers/`
- Views (Razor) in `OnlineJewelryStore/Views/`
- Models in `OnlineJewelryStore/Models/` (currently empty, ready for implementation)
- App configuration in `OnlineJewelryStore/App_Start/`
- Static content (CSS/JS) in `OnlineJewelryStore/Content/` and `OnlineJewelryStore/Scripts/`

**Key Dependencies**:
- ASP.NET MVC 5.2.9
- Bootstrap 5.2.3
- jQuery 3.7.0
- Newtonsoft.Json 13.0.3

## Build and Run

**Build the solution**:
```bash
msbuild OnlineJewelryStore.sln /p:Configuration=Debug
```

**Build for release**:
```bash
msbuild OnlineJewelryStore.sln /p:Configuration=Release
```

**Run with IIS Express** (default URL: https://localhost:44363/):
- Open in Visual Studio and press F5, or
- Use IIS Express directly: `"C:\Program Files\IIS Express\iisexpress.exe" /path:"D:\WorkSpace\ASP.NET_projects\OnlineJewelryStore\OnlineJewelryStore" /port:44363`

**Restore NuGet packages**:
```bash
nuget restore OnlineJewelryStore.sln
```

## Application Structure

**Global Application Class** (`Global.asax.cs`):
- Application entry point that registers areas, filters, routes, and bundles
- Initializes configuration from `App_Start/` classes

**Routing** (`App_Start/RouteConfig.cs`):
- Default route: `{controller}/{action}/{id}`
- Default controller: `Home`, default action: `Index`

**Configuration Files**:
- `Web.config` - Main application configuration
- `Web.Debug.config` - Debug environment transformations
- `Web.Release.config` - Release environment transformations

**Current Implementation**:
- Single `HomeController` with Index, About, and Contact actions
- No models or data layer implemented yet
- No database connection configured

## Development Notes

This is a fresh MVC project scaffold - the Models folder is empty and ready for domain model implementation. When adding data access:
- Consider adding Entity Framework via NuGet if ORM is needed
- Add connection strings to `Web.config`
- Create model classes in `OnlineJewelryStore/Models/`
- Consider adding a separate data access layer or repository pattern

When adding new controllers, follow the existing pattern in `Controllers/HomeController.cs` and ensure corresponding views are created in `Views/{ControllerName}/`.
