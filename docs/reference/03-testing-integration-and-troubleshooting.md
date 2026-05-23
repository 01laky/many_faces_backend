# Testing, integration, and troubleshooting

## Unit / integration tests

```bash
cd many_faces_backend
dotnet test BeDemo.Api.Tests/BeDemo.Api.Tests.csproj
```

| Area | Typical test location |
| ---- | --------------------- |
| Platform ACL / SUPER_ADMIN | `PlatformSuperAdminAccessEdgeTests.cs`, `PlatformAccessRulesTests.cs` |
| Capabilities | `AccessCapabilitiesServiceTests.cs`, `AclIntegrationTests.cs` |
| Operator infra | `AdminInfraControllerTests.cs` |
| Content moderation | `ContentModeration*Tests.cs` |

Monorepo matrix: [`testing-and-ci-matrix.md`](../../../docs/guides/testing-and-ci-matrix.md).

## Integration test accounts

| Email | Role | Use |
| ----- | ---- | --- |
| `integration-superadmin@test.com` | `SUPER_ADMIN` | Admin-face **200** paths |
| `integration-admin@test.com` | `ADMIN` | Admin-face **403** negatives |
| `integration-user@test.com` | `USER` | Tenant paths |

See [`local-dev-accounts.md`](../../../docs/guides/local-dev-accounts.md).

## Common failures

| Symptom | Check |
| ------- | ----- |
| **403 on `/admin/api`** with `admin1@demo.com` | Expected — use **`admin@admin.com`** ([`admin-superadmin-only-access.md`](../../../docs/guides/admin-superadmin-only-access.md)) |
| **401** after idle | JWT expired — refresh grant or re-login |
| Wrong face prefix | [`02-routing-config-and-workflow.md`](./02-routing-config-and-workflow.md) |
| Worker unreachable | [`troubleshooting-local-dev.md`](../../../docs/guides/troubleshooting-local-dev.md) |

## CI

Backend repo runs `dotnet format` + `dotnet test` on push. Monorepo `./scripts/lint-all.sh` includes backend format check.
