"use client";

import { AppShell } from "@/components/app-shell";
import { useAuth } from "@/components/auth-provider";

export default function SettingsPage() {
  const { user } = useAuth();

  return (
    <AppShell>
      <h1 className="mb-4 text-2xl font-semibold">Settings</h1>
      <div className="max-w-xl space-y-4 rounded-lg border border-slate-200 bg-white p-5 text-sm">
        <div>
          <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">Organization</div>
          <div className="mt-1 font-mono text-xs">{user?.organizationId}</div>
        </div>
        <div>
          <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">Branch</div>
          <div className="mt-1 font-mono text-xs">{user?.branchId ?? "—"}</div>
        </div>
        <div>
          <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">Signed in as</div>
          <div className="mt-1">{user?.displayName} ({user?.email})</div>
        </div>
        <div>
          <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">Roles</div>
          <div className="mt-1">{user?.roles.join(", ") || "—"}</div>
        </div>
        <div>
          <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">Permissions</div>
          <ul className="mt-2 grid gap-1 sm:grid-cols-2">
            {user?.permissions.map((p) => (
              <li key={p} className="rounded bg-slate-50 px-2 py-1 font-mono text-xs">{p}</li>
            ))}
          </ul>
        </div>
        <p className="text-slate-500">
          Repair statuses, payment methods, and tax settings expand in later phases. Phase 1 seeds configurable repair statuses and RBAC permissions.
        </p>
      </div>
    </AppShell>
  );
}
