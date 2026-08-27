"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { AppShell } from "@/components/app-shell";
import { api, ApiClientError } from "@/lib/api";
import { FormEvent, useEffect, useState } from "react";

type PaymentMethod = { id: string; code: string; name: string };
type SaleDetail = {
  id: string;
  saleNumber: string;
  customerName?: string | null;
  status: string;
  subtotal: number;
  discountTotal: number;
  taxTotal: number;
  totalAmount: number;
  amountPaid: number;
  balanceDue: number;
  completedAt?: string | null;
  notes?: string | null;
  lines: {
    id: string;
    partId: string;
    description: string;
    quantity: number;
    unitPrice: number;
    discount: number;
    lineTotal: number;
  }[];
  payments: {
    id: string;
    amount: number;
    methodCode: string;
    paidAt: string;
    status: string;
  }[];
  invoice?: {
    id: string;
    invoiceNumber: string;
    status: string;
    balanceDue: number;
  } | null;
  returns: {
    id: string;
    returnNumber: string;
    refundAmount: number;
    completedAt: string;
    lines: { saleLineId: string; quantity: number }[];
  }[];
};

const fieldClass = "w-full rounded-md border border-slate-300 px-3 py-2 text-sm";
const labelClass = "mb-1 block text-xs font-medium text-slate-600";

function newIdempotencyKey() {
  return crypto.randomUUID();
}

export default function SaleDetailPage() {
  const params = useParams<{ id: string }>();
  const [sale, setSale] = useState<SaleDetail | null>(null);
  const [methods, setMethods] = useState<PaymentMethod[]>([]);
  const [payAmount, setPayAmount] = useState("");
  const [methodCode, setMethodCode] = useState("CASH");
  const [returnLineId, setReturnLineId] = useState("");
  const [returnQty, setReturnQty] = useState("1");
  const [refundAmount, setRefundAmount] = useState("");
  const [error, setError] = useState<string | null>(null);

  async function load() {
    const [s, m] = await Promise.all([
      api<SaleDetail>(`/api/v1/sales/${params.id}`),
      api<PaymentMethod[]>("/api/v1/payment-methods"),
    ]);
    setSale(s);
    setMethods(m);
    if (m[0]) setMethodCode(m[0].code);
    if (s.lines[0] && !returnLineId) setReturnLineId(s.lines[0].id);
    if (s.balanceDue > 0) setPayAmount(String(s.balanceDue));
  }

  useEffect(() => {
    load().catch((e) => setError(e.message));
  }, [params.id]);

  async function recordPayment(e: FormEvent) {
    e.preventDefault();
    if (!sale) return;
    setError(null);
    try {
      await api(`/api/v1/sales/${sale.id}/payments`, {
        method: "POST",
        body: JSON.stringify({
          amount: Number(payAmount),
          methodCode,
          idempotencyKey: newIdempotencyKey(),
        }),
      });
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Payment failed");
    }
  }

  async function createReturn(e: FormEvent) {
    e.preventDefault();
    if (!sale) return;
    setError(null);
    try {
      await api(`/api/v1/sales/${sale.id}/returns`, {
        method: "POST",
        body: JSON.stringify({
          lines: [{ saleLineId: returnLineId, quantity: Number(returnQty) }],
          refundAmount: refundAmount ? Number(refundAmount) : null,
          refundMethodCode: methodCode,
          idempotencyKey: newIdempotencyKey(),
        }),
      });
      setReturnQty("1");
      setRefundAmount("");
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Return failed");
    }
  }

  async function voidSale() {
    if (!sale || !confirm("Void this unpaid sale and restock?")) return;
    setError(null);
    try {
      await api(`/api/v1/sales/${sale.id}/void`, { method: "POST" });
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Void failed");
    }
  }

  return (
    <AppShell>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <div>
          <Link href="/sales" className="text-sm text-slate-500 underline">
            Sales
          </Link>
          <h1 className="text-2xl font-semibold">{sale?.saleNumber ?? "Sale"}</h1>
        </div>
        {sale?.status === "COMPLETED" && sale.amountPaid <= 0 && (
          <button
            type="button"
            onClick={() => void voidSale()}
            className="rounded-md border border-red-300 px-3 py-2 text-sm text-red-700"
          >
            Void sale
          </button>
        )}
      </div>
      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
      {!sale && !error && <p className="text-sm text-slate-500">Loading…</p>}
      {sale && (
        <div className="space-y-4">
          <div className="grid gap-3 rounded-lg border border-slate-200 bg-white p-4 sm:grid-cols-2 lg:grid-cols-4">
            <div>
              <div className="text-xs uppercase text-slate-500">Status</div>
              <div className="font-medium">{sale.status}</div>
            </div>
            <div>
              <div className="text-xs uppercase text-slate-500">Customer</div>
              <div className="font-medium">{sale.customerName ?? "Walk-in"}</div>
            </div>
            <div>
              <div className="text-xs uppercase text-slate-500">Total</div>
              <div className="font-mono">₱{sale.totalAmount.toLocaleString()}</div>
            </div>
            <div>
              <div className="text-xs uppercase text-slate-500">Balance due</div>
              <div className="font-mono">₱{sale.balanceDue.toLocaleString()}</div>
            </div>
            {sale.invoice && (
              <div className="sm:col-span-2">
                <div className="text-xs uppercase text-slate-500">Invoice</div>
                <div>
                  {sale.invoice.invoiceNumber} — {sale.invoice.status}
                </div>
              </div>
            )}
          </div>

          <div className="rounded-lg border border-slate-200 bg-white p-4">
            <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">Lines</h2>
            <table className="w-full text-left text-sm">
              <thead className="text-xs uppercase text-slate-500">
                <tr>
                  <th className="py-2">Item</th>
                  <th className="py-2">Qty</th>
                  <th className="py-2">Price</th>
                  <th className="py-2 text-right">Total</th>
                </tr>
              </thead>
              <tbody>
                {sale.lines.map((l) => (
                  <tr key={l.id} className="border-t border-slate-100">
                    <td className="py-2">{l.description}</td>
                    <td className="py-2 font-mono">{l.quantity}</td>
                    <td className="py-2 font-mono">₱{l.unitPrice}</td>
                    <td className="py-2 text-right font-mono">₱{l.lineTotal.toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="rounded-lg border border-slate-200 bg-white p-4">
            <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">Payments</h2>
            <ul className="mb-4 space-y-1 text-sm">
              {sale.payments.map((p) => (
                <li key={p.id} className="flex justify-between border-b border-slate-50 py-1">
                  <span>
                    {p.methodCode} · {p.status}
                  </span>
                  <span className="font-mono">₱{p.amount.toLocaleString()}</span>
                </li>
              ))}
              {sale.payments.length === 0 && <li className="text-slate-500">No payments yet.</li>}
            </ul>
            {sale.status === "COMPLETED" && sale.balanceDue > 0 && (
              <form onSubmit={recordPayment} className="grid gap-3 md:grid-cols-3">
                <div>
                  <label className={labelClass}>Amount</label>
                  <input
                    type="number"
                    step="0.01"
                    min="0.01"
                    value={payAmount}
                    onChange={(e) => setPayAmount(e.target.value)}
                    required
                    className={fieldClass}
                  />
                </div>
                <div>
                  <label className={labelClass}>Method</label>
                  <select value={methodCode} onChange={(e) => setMethodCode(e.target.value)} className={fieldClass}>
                    {methods.map((m) => (
                      <option key={m.id} value={m.code}>
                        {m.name}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="flex items-end">
                  <button className="w-full rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white">
                    Record payment
                  </button>
                </div>
              </form>
            )}
          </div>

          {sale.status === "COMPLETED" && (
            <div className="rounded-lg border border-slate-200 bg-white p-4">
              <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">Return</h2>
              <form onSubmit={createReturn} className="grid gap-3 md:grid-cols-4">
                <div>
                  <label className={labelClass}>Sale line</label>
                  <select
                    value={returnLineId}
                    onChange={(e) => setReturnLineId(e.target.value)}
                    className={fieldClass}
                  >
                    {sale.lines.map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.description}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className={labelClass}>Quantity</label>
                  <input
                    type="number"
                    step="0.01"
                    min="0.01"
                    value={returnQty}
                    onChange={(e) => setReturnQty(e.target.value)}
                    required
                    className={fieldClass}
                  />
                </div>
                <div>
                  <label className={labelClass}>Refund amount (optional)</label>
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    value={refundAmount}
                    onChange={(e) => setRefundAmount(e.target.value)}
                    className={fieldClass}
                  />
                </div>
                <div className="flex items-end">
                  <button className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm">
                    Create return
                  </button>
                </div>
              </form>
              {sale.returns.length > 0 && (
                <ul className="mt-4 space-y-1 text-sm text-slate-600">
                  {sale.returns.map((r) => (
                    <li key={r.id}>
                      {r.returnNumber} — refund ₱{r.refundAmount.toLocaleString()}
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
        </div>
      )}
    </AppShell>
  );
}
