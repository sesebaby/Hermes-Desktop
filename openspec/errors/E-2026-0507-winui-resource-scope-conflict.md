# E-2026-0507-winui-resource-scope-conflict

- id: E-2026-0507
- title: WinUI ResourceLoader lookups must not create parent resource/scope conflicts
- status: active
- updated_at: 2026-05-07
- keywords: [winui, mrtcore, resw, ResourceLoader, NamedResource, PRI278]
- trigger_scope: [desktop, localization, bugfix, build]

## Symptoms

- Clicking a developer page clear-filter path threw `System.Runtime.InteropServices.COMException` with `HResult=0x80073B17` and `NamedResource` missing.
- Adding a bare `.resw` key to match `ResourceLoader.GetString("DeveloperDiagnosticsExportInitial")` made the WinUI build fail with `PRI278`, because `DeveloperDiagnosticsExportInitial.Text` already existed for `x:Uid`.

## Root Cause

- MRTCore treats dotted resource names as scopes. A `.resw` entry named `DeveloperDiagnosticsExportInitial.Text` is loaded from code with `ResourceLoader.GetString("DeveloperDiagnosticsExportInitial/Text")`, not by defining a second parent resource named `DeveloperDiagnosticsExportInitial`.
- Defining both `Foo` and `Foo.Text` makes `Foo` both a resource and a scope, which PRI generation rejects.

## Bad Fix Paths

- Do not fix a code-behind `NamedResource` miss by blindly adding a bare key when a matching `x:Uid` property resource already exists.
- Do not duplicate `.Text` values under both `Foo.Text` and `Foo`; it may pass a unit-level XML check but fail PRI generation.

## Corrective Constraints

- For code-behind reads of XAML property resources, use slash syntax such as `ResourceLoader.GetString("Foo/Text")` for `.resw` key `Foo.Text`.
- Resource coverage tests should normalize `/` to `.` when checking `.resw` names, and build verification must run after localization fixes to catch PRI scope conflicts.

## Verification Evidence

- `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~HermesDesktop.Tests.Views.DeveloperPageResourceTests.CodeBehindResourceLookups_AllExistInSupportedLocales"`
- `dotnet build .\Desktop\HermesDesktop\HermesDesktop.csproj -c Debug -p:Platform=x64`

## Related Files

- `Desktop/HermesDesktop/Views/DeveloperPage.xaml.cs`
- `Desktop/HermesDesktop/Strings/en-us/Resources.resw`
- `Desktop/HermesDesktop/Strings/zh-cn/Resources.resw`
- `Desktop/HermesDesktop.Tests/Views/DeveloperPageResourceTests.cs`
