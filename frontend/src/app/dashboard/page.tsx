"use client";

import { AppShell } from "@/components/app-shell";
import { api } from "@/lib/api";
import { useEffect, useState } from "react";

type Dashboard = {
  todayCompletedRepairRevenue: number;
  pendingRepairs: number;
  overdueRepairs: number;
  completedToday: number;
  lowStockParts: number;
  technicianWorkload: { technicianUserId?: string | null; openRepairs: number }[];
  unavailable: {
    sales: string;
    expenses: string;
    cashBalance: string;
    unpaidInvoices: string;
  };
};

function Metric({
  label,
  value,
  hint,
}: {
  label: string;
  value: string;
  hint?: string;
}) {
  return (
    <div className="rounded-lg border border-slate-200 bg-white p-4">
      <div className="text-xs font-medium uppercase tracking-wide text-slate-500">
        {label}
      </div>
      <div className="mt-2 font-mono text-2xl font-semibold text-slate-900">
        {value}
      </div>
      {hint ? <div className="mt-1 text-xs text-slate-500">{hint}</div> : null}
    </div>
  );
}

export default function DashboardPage() {
  const [data, setData] = useState<Dashboard | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api<Dashboard>("/api/v1/dashboard")
      .then(setData)
      .catch((e) => setError(e.message));
  }, []);

  return (
    <AppShell>
      <h1 className="mb-4 text-2xl font-semibold">Dashboard</h1>
      {error && <p className="text-sm text-red-600">{error}</p>}
      {!data && !error && <p className="text-sm text-slate-500">Loading…</p>}
      {data && (
        <>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <Metric
              label="Today repair revenue"
              value={`₱${data.todayCompletedRepairRevenue.toLocaleString()}`}
              hint="Completed repairs today"
            />
            <Metric label="Pending repairs" value={String(data.pendingRepairs)} />
            <Metric label="Overdue repairs" value={String(data.overdueRepairs)} />
            <Metric label="Completed today" value={String(data.completedToday)} />
          </div>
          <div className="mt-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            <Metric label="Low stock parts" value={String(data.lowStockParts)} hint="On hand below reorder level" />
            <Metric label="Sales" value="—" hint={data.unavailable.sales} />
            <Metric label="Unpaid invoices" value="—" hint={data.unavailable.unpaidInvoices} />
            <Metric label="Expenses" value="—" hint={data.unavailable.expenses} />
            <Metric label="Cash balance" value="—" hint={data.unavailable.cashBalance} />
          </div>
          <div className="mt-6 rounded-lg border border-slate-200 bg-white p-4">
            <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">
              Technician workload
            </h2>
            {data.technicianWorkload.length === 0 ? (
              <p className="text-sm text-slate-500">No open assigned repairs.</p>
            ) : (
              <ul className="space-y-2 text-sm">
                {data.technicianWorkload.map((t, i) => (
                  <li key={i} className="flex justify-between border-b border-slate-100 py-1">
                    <span>{t.technicianUserId ?? "Unassigned"}</span>
                    <span className="font-mono">{t.openRepairs}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </>
      )}
    </AppShell>
  );
}
