"use client";

import { AppShell } from "@/components/app-shell";
import { useAuth } from "@/components/auth-provider";
import { api, ApiClientError } from "@/lib/api";
import { formatOrgDateTime } from "@/lib/datetime";
import { FormEvent, useState } from "react";

type ReportKind =
  | "trial-balance"
  | "profit-and-loss"
  | "balance-sheet"
  | "general-ledger"
  | "cash-flow"
  | "ar-aging"
  | "ap-aging";

const REPORTS: { id: ReportKind; label: string; needsRange: boolean }[] = [
  { id: "trial-balance", label: "Trial balance", needsRange: false },
  { id: "profit-and-loss", label: "P&L", needsRange: true },
  { id: "balance-sheet", label: "Balance sheet", needsRange: false },
  { id: "general-ledger", label: "General ledger", needsRange: true },
  { id: "cash-flow", label: "Cash flow", needsRange: true },
  { id: "ar-aging", label: "AR aging", needsRange: false },
  { id: "ap-aging", label: "AP aging", needsRange: false },
];

const fieldClass = "rounded-md border border-slate-300 px-3 py-2 text-sm";
const labelClass = "mb-1 block text-xs font-medium text-slate-600";

function todayDateInput() {
  return new Date().toISOString().slice(0, 10);
}

function monthStartDateInput() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

function toIsoStart(date: string) {
  return new Date(`${date}T00:00:00`).toISOString();
}

function toIsoEnd(date: string) {
  return new Date(`${date}T23:59:59.999`).toISOString();
}

export default function ReportsPage() {
  const { user } = useAuth();
  const tz = user?.timeZoneId;
  const [kind, setKind] = useState<ReportKind>("trial-balance");
  const [from, setFrom] = useState(monthStartDateInput);
  const [to, setTo] = useState(todayDateInput);
  const [asOf, setAsOf] = useState(todayDateInput);
  const [data, setData] = useState<unknown>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const meta = REPORTS.find((r) => r.id === kind)!;

  async function onFetch(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      let path = "";
      if (meta.needsRange) {
        path = `/api/v1/accounting/reports/${kind}?from=${encodeURIComponent(toIsoStart(from))}&to=${encodeURIComponent(toIsoEnd(to))}`;
      } else {
        path = `/api/v1/accounting/reports/${kind}?asOf=${encodeURIComponent(toIsoEnd(asOf))}`;
      }
      const result = await api<unknown>(path);
      setData(result);
    } catch (err: unknown) {
      setData(null);
      setError(err instanceof ApiClientError ? err.message : "Failed");
    } finally {
      setLoading(false);
    }
  }

  return (
    <AppShell>
      <h1 className="mb-4 text-2xl font-semibold">Accounting reports</h1>
      <form
        onSubmit={onFetch}
        className="mb-4 flex flex-wrap items-end gap-3 rounded-lg border border-slate-200 bg-white p-4"
      >
        <div>
          <label className={labelClass}>Report</label>
          <select
            value={kind}
            onChange={(e) => setKind(e.target.value as ReportKind)}
            className={fieldClass}
          >
            {REPORTS.map((r) => (
              <option key={r.id} value={r.id}>
                {r.label}
              </option>
            ))}
          </select>
        </div>
        {meta.needsRange ? (
          <>
            <div>
              <label className={labelClass}>From</label>
              <input
                type="date"
                value={from}
                onChange={(e) => setFrom(e.target.value)}
                required
                className={fieldClass}
              />
            </div>
            <div>
              <label className={labelClass}>To</label>
              <input
                type="date"
                value={to}
                onChange={(e) => setTo(e.target.value)}
                required
                className={fieldClass}
              />
            </div>
          </>
        ) : (
          <div>
            <label className={labelClass}>As of</label>
            <input
              type="date"
              value={asOf}
              onChange={(e) => setAsOf(e.target.value)}
              required
              className={fieldClass}
            />
          </div>
        )}
        <button
          disabled={loading}
          className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
        >
          {loading ? "Loading…" : "Run report"}
        </button>
      </form>
      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
      {data != null && <ReportResult kind={kind} data={data} tz={tz} />}
    </AppShell>
  );
}

function money(n: number) {
  return `₱${n.toLocaleString()}`;
}

function ReportResult({
  kind,
  data,
  tz,
}: {
  kind: ReportKind;
  data: unknown;
  tz?: string;
}) {
  if (kind === "trial-balance") {
    const rows = data as {
      accountId: string;
      code: string;
      name: string;
      accountType: string;
      debitTotal: number;
      creditTotal: number;
      balance: number;
    }[];
    return (
      <SimpleTable
        headers={["Code", "Name", "Type", "Debit", "Credit", "Balance"]}
        rows={rows.map((r) => [
          r.code,
          r.name,
          r.accountType,
          money(r.debitTotal),
          money(r.creditTotal),
          money(r.balance),
        ])}
      />
    );
  }

  if (kind === "profit-and-loss") {
    const r = data as {
      from: string;
      to: string;
      revenue: { code: string; name: string; amount: number }[];
      costOfGoodsSold: { code: string; name: string; amount: number }[];
      expenses: { code: string; name: string; amount: number }[];
      totalRevenue: number;
      totalCogs: number;
      totalExpenses: number;
      netIncome: number;
    };
    const lines = [
      ...r.revenue.map((x) => ["Revenue", x.code, x.name, money(x.amount)]),
      ...r.costOfGoodsSold.map((x) => ["COGS", x.code, x.name, money(x.amount)]),
      ...r.expenses.map((x) => ["Expense", x.code, x.name, money(x.amount)]),
      ["", "", "Total revenue", money(r.totalRevenue)],
      ["", "", "Total COGS", money(r.totalCogs)],
      ["", "", "Total expenses", money(r.totalExpenses)],
      ["", "", "Net income", money(r.netIncome)],
    ];
    return (
      <div>
        <p className="mb-2 text-sm text-slate-600">
          {formatOrgDateTime(r.from, tz)} → {formatOrgDateTime(r.to, tz)}
        </p>
        <SimpleTable headers={["Section", "Code", "Name", "Amount"]} rows={lines} />
      </div>
    );
  }

  if (kind === "balance-sheet") {
    const r = data as {
      asOf: string;
      assets: { code: string; name: string; amount: number }[];
      liabilities: { code: string; name: string; amount: number }[];
      equity: { code: string; name: string; amount: number }[];
      totalAssets: number;
      totalLiabilities: number;
      totalEquity: number;
      retainedEarnings: number;
      totalLiabilitiesAndEquity: number;
    };
    const lines = [
      ...r.assets.map((x) => ["Asset", x.code, x.name, money(x.amount)]),
      ...r.liabilities.map((x) => ["Liability", x.code, x.name, money(x.amount)]),
      ...r.equity.map((x) => ["Equity", x.code, x.name, money(x.amount)]),
      ["", "", "Retained earnings", money(r.retainedEarnings)],
      ["", "", "Total assets", money(r.totalAssets)],
      ["", "", "Total liabilities", money(r.totalLiabilities)],
      ["", "", "Total equity", money(r.totalEquity)],
      ["", "", "Total L+E", money(r.totalLiabilitiesAndEquity)],
    ];
    return (
      <div>
        <p className="mb-2 text-sm text-slate-600">
          As of {formatOrgDateTime(r.asOf, tz)}
        </p>
        <SimpleTable headers={["Section", "Code", "Name", "Amount"]} rows={lines} />
      </div>
    );
  }

  if (kind === "general-ledger" || kind === "cash-flow") {
    const rows = data as {
      accountCode: string;
      accountName: string;
      entryNumber: string;
      entryDate: string;
      description?: string | null;
      memo?: string | null;
      debit: number;
      credit: number;
      runningBalance?: number;
      netChange?: number;
    }[];
    return (
      <SimpleTable
        headers={
          kind === "general-ledger"
            ? ["Account", "Entry", "Date", "Desc", "Debit", "Credit", "Balance"]
            : ["Account", "Entry", "Date", "Memo", "Debit", "Credit", "Net"]
        }
        rows={rows.map((r) => [
          `${r.accountCode} ${r.accountName}`,
          r.entryNumber,
          formatOrgDateTime(r.entryDate, tz),
          kind === "general-ledger" ? r.description ?? "—" : r.memo ?? "—",
          money(r.debit),
          money(r.credit),
          money(kind === "general-ledger" ? r.runningBalance ?? 0 : r.netChange ?? 0),
        ])}
      />
    );
  }

  const aging = data as {
    asOf: string;
    rows: {
      partyName: string;
      documentNumber: string;
      anchorDate: string;
      daysPastDue: number;
      balanceDue: number;
      bucket: string;
    }[];
    current: number;
    days1To30: number;
    days31To60: number;
    days61To90: number;
    days90Plus: number;
    total: number;
  };
  return (
    <div>
      <p className="mb-2 text-sm text-slate-600">
        As of {formatOrgDateTime(aging.asOf, tz)} · Total {money(aging.total)} · Current{" "}
        {money(aging.current)} · 1–30 {money(aging.days1To30)} · 31–60{" "}
        {money(aging.days31To60)} · 61–90 {money(aging.days61To90)} · 90+{" "}
        {money(aging.days90Plus)}
      </p>
      <SimpleTable
        headers={["Party", "Document", "Anchor", "Days", "Bucket", "Balance"]}
        rows={aging.rows.map((r) => [
          r.partyName,
          r.documentNumber,
          formatOrgDateTime(r.anchorDate, tz),
          String(r.daysPastDue),
          r.bucket,
          money(r.balanceDue),
        ])}
      />
    </div>
  );
}

function SimpleTable({
  headers,
  rows,
}: {
  headers: string[];
  rows: string[][];
}) {
  return (
    <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
      <table className="w-full text-left text-sm">
        <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase text-slate-500">
          <tr>
            {headers.map((h) => (
              <th key={h} className="px-4 py-3">
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, i) => (
            <tr key={i} className="border-b border-slate-100">
              {row.map((cell, j) => (
                <td
                  key={j}
                  className={`px-4 py-3 ${j === row.length - 1 || cell.startsWith("₱") ? "font-mono text-xs" : ""}`}
                >
                  {cell}
                </td>
              ))}
            </tr>
          ))}
          {rows.length === 0 && (
            <tr>
              <td
                colSpan={headers.length}
                className="px-4 py-8 text-center text-slate-500"
              >
                No rows.
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
