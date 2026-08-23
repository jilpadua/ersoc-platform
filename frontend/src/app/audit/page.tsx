"use client";

import { AppShell } from "@/components/app-shell";
import { api, Paged } from "@/lib/api";
import { useEffect, useState } from "react";

type AuditLog = {
  id: string;
  action: string;
  entityType: string;
  entityId: string;
  actorUserId?: string;
  timestamp: string;
};

export default function AuditPage() {
  const [items, setItems] = useState<AuditLog[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api<Paged<AuditLog>>("/api/v1/audit-logs?pageSize=100")
      .then((d) => setItems(d.items))
      .catch((e) => setError(e.message));
  }, []);

  return (
    <AppShell>
      <h1 className="mb-4 text-2xl font-semibold">Audit logs</h1>
      <p className="mb-4 text-sm text-slate-600">
        Append-only record of important business changes. Normal users cannot modify these entries.
      </p>
      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">When</th>
              <th className="px-4 py-3">Action</th>
              <th className="px-4 py-3">Entity</th>
              <th className="px-4 py-3">Id</th>
            </tr>
          </thead>
          <tbody>
            {items.map((a) => (
              <tr key={a.id} className="border-t border-slate-100">
                <td className="px-4 py-3 font-mono text-xs">{new Date(a.timestamp).toLocaleString()}</td>
                <td className="px-4 py-3">{a.action}</td>
                <td className="px-4 py-3">{a.entityType}</td>
                <td className="px-4 py-3 font-mono text-xs">{a.entityId}</td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr><td colSpan={4} className="px-4 py-8 text-center text-slate-500">No audit events yet.</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </AppShell>
  );
}
