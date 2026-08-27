"use client";

import Link from "next/link";
import { AppShell } from "@/components/app-shell";
import { useAuth } from "@/components/auth-provider";
import { api, Paged } from "@/lib/api";
import { formatOrgDateTime } from "@/lib/datetime";
import { useEffect, useState } from "react";

type Invoice = {
  id: string;
  saleId: string;
  invoiceNumber: string;
  status: string;
  issuedAt: string;
  dueAt?: string | null;
  voidedAt?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  totalAmount: number;
  amountPaid: number;
  balanceDue: number;
};

export default function InvoicesPage() {
  const { user } = useAuth();
  const tz = user?.timeZoneId;
  const [items, setItems] = useState<Invoice[]>([]);
  const [unpaidOnly, setUnpaidOnly] = useState(true);
  const [error, setError] = useState<string | null>(null);

  async function load(unpaid = unpaidOnly) {
    const data = await api<Paged<Invoice>>(
      `/api/v1/invoices?pageSize=50&unpaidOnly=${unpaid}`
    );
    setItems(data.items);
  }

  useEffect(() => {
    load().catch((e) => setError(e.message));
  }, []);

  return (
    <AppShell>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <h1 className="text-2xl font-semibold">Invoices</h1>
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <input
            type="checkbox"
            checked={unpaidOnly}
            onChange={(e) => {
              const v = e.target.checked;
              setUnpaidOnly(v);
              load(v).catch((err) => setError(err.message));
            }}
          />
          Unpaid / partial only
        </label>
      </div>
      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">Invoice #</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3">Issued</th>
              <th className="px-4 py-3 text-right">Total</th>
              <th className="px-4 py-3 text-right">Paid</th>
              <th className="px-4 py-3 text-right">Balance</th>
              <th className="px-4 py-3">Sale</th>
            </tr>
          </thead>
          <tbody>
            {items.map((i) => (
              <tr key={i.id} className="border-b border-slate-100">
                <td className="px-4 py-3 font-medium">{i.invoiceNumber}</td>
                <td className="px-4 py-3">{i.status}</td>
                <td className="px-4 py-3 font-mono text-xs">
                  {formatOrgDateTime(i.issuedAt, tz)}
                </td>
                <td className="px-4 py-3 text-right font-mono">₱{i.totalAmount.toLocaleString()}</td>
                <td className="px-4 py-3 text-right font-mono">₱{i.amountPaid.toLocaleString()}</td>
                <td className="px-4 py-3 text-right font-mono">₱{i.balanceDue.toLocaleString()}</td>
                <td className="px-4 py-3">
                  <Link href={`/sales/${i.saleId}`} className="underline">
                    Open sale
                  </Link>
                </td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr>
                <td colSpan={7} className="px-4 py-8 text-center text-slate-500">
                  No invoices match.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </AppShell>
  );
}
