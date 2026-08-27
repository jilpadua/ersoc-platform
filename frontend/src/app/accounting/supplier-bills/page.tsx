"use client";

import { AppShell } from "@/components/app-shell";
import { useAuth } from "@/components/auth-provider";
import { api, ApiClientError, Paged } from "@/lib/api";
import { formatOrgDateTime } from "@/lib/datetime";
import { newIdempotencyKey } from "@/lib/id";
import { FormEvent, useEffect, useState } from "react";

type Bill = {
  id: string;
  supplierId: string;
  supplierName?: string | null;
  billNumber: string;
  totalAmount: number;
  amountPaid: number;
  balanceDue: number;
  status: string;
  issuedAt: string;
  notes?: string | null;
};

type PaymentMethod = { id: string; code: string; name: string };

const fieldClass = "w-full rounded-md border border-slate-300 px-3 py-2 text-sm";
const labelClass = "mb-1 block text-xs font-medium text-slate-600";

export default function SupplierBillsPage() {
  const { user } = useAuth();
  const tz = user?.timeZoneId;
  const [items, setItems] = useState<Bill[]>([]);
  const [methods, setMethods] = useState<PaymentMethod[]>([]);
  const [unpaidOnly, setUnpaidOnly] = useState(true);
  const [billId, setBillId] = useState("");
  const [amount, setAmount] = useState("");
  const [methodCode, setMethodCode] = useState("CASH");
  const [idempotencyKey, setIdempotencyKey] = useState(newIdempotencyKey);
  const [error, setError] = useState<string | null>(null);

  async function load(unpaid = unpaidOnly) {
    const [bills, m] = await Promise.all([
      api<Paged<Bill>>(
        `/api/v1/supplier-bills?pageSize=50&unpaidOnly=${unpaid}`
      ),
      api<PaymentMethod[]>("/api/v1/payment-methods"),
    ]);
    setItems(bills.items);
    setMethods(m);
    if (m[0] && methodCode === "CASH") setMethodCode(m[0].code);
  }

  useEffect(() => {
    load().catch((e) => setError(e.message));
  }, []);

  function selectBill(b: Bill) {
    setBillId(b.id);
    setAmount(String(b.balanceDue));
  }

  async function onPay(e: FormEvent) {
    e.preventDefault();
    setError(null);
    const bill = items.find((b) => b.id === billId);
    if (!bill) {
      setError("Select a bill.");
      return;
    }
    try {
      const payAmount = Number(amount);
      await api("/api/v1/supplier-payments", {
        method: "POST",
        body: JSON.stringify({
          supplierId: bill.supplierId,
          amount: payAmount,
          methodCode,
          idempotencyKey,
          allocations: [{ billId: bill.id, amount: payAmount }],
        }),
      });
      setIdempotencyKey(newIdempotencyKey());
      setBillId("");
      setAmount("");
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  return (
    <AppShell>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <h1 className="text-2xl font-semibold">Supplier bills</h1>
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
          Unpaid only
        </label>
      </div>
      <form
        onSubmit={onPay}
        className="mb-4 grid gap-3 rounded-lg border border-slate-200 bg-white p-4 md:grid-cols-4"
      >
        <div>
          <label className={labelClass}>Bill</label>
          <select
            value={billId}
            onChange={(e) => {
              const id = e.target.value;
              setBillId(id);
              const b = items.find((x) => x.id === id);
              if (b) setAmount(String(b.balanceDue));
            }}
            required
            className={fieldClass}
          >
            <option value="">Select bill</option>
            {items.map((b) => (
              <option key={b.id} value={b.id}>
                {b.billNumber} — {b.supplierName ?? "Supplier"} (₱
                {b.balanceDue.toLocaleString()})
              </option>
            ))}
          </select>
        </div>
        <div>
          <label className={labelClass}>Amount</label>
          <input
            type="number"
            min="0.01"
            step="0.01"
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            required
            className={fieldClass}
          />
        </div>
        <div>
          <label className={labelClass}>Method</label>
          <select
            value={methodCode}
            onChange={(e) => setMethodCode(e.target.value)}
            className={fieldClass}
          >
            {methods.length === 0 ? (
              <>
                <option value="CASH">Cash</option>
                <option value="CARD">Card</option>
                <option value="TRANSFER">Transfer</option>
              </>
            ) : (
              methods.map((m) => (
                <option key={m.id} value={m.code}>
                  {m.name}
                </option>
              ))
            )}
          </select>
        </div>
        <div>
          <label className={labelClass}>Idempotency key</label>
          <input
            value={idempotencyKey}
            onChange={(e) => setIdempotencyKey(e.target.value)}
            required
            className={fieldClass}
          />
        </div>
        <div className="flex items-end md:col-span-4">
          <button className="rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white">
            Record payment
          </button>
        </div>
      </form>
      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">Bill #</th>
              <th className="px-4 py-3">Supplier</th>
              <th className="px-4 py-3">Issued</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3 text-right">Total</th>
              <th className="px-4 py-3 text-right">Paid</th>
              <th className="px-4 py-3 text-right">Balance</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {items.map((b) => (
              <tr key={b.id} className="border-b border-slate-100">
                <td className="px-4 py-3 font-medium">{b.billNumber}</td>
                <td className="px-4 py-3">{b.supplierName ?? "—"}</td>
                <td className="px-4 py-3 font-mono text-xs">
                  {formatOrgDateTime(b.issuedAt, tz)}
                </td>
                <td className="px-4 py-3">{b.status}</td>
                <td className="px-4 py-3 text-right font-mono">
                  ₱{b.totalAmount.toLocaleString()}
                </td>
                <td className="px-4 py-3 text-right font-mono">
                  ₱{b.amountPaid.toLocaleString()}
                </td>
                <td className="px-4 py-3 text-right font-mono">
                  ₱{b.balanceDue.toLocaleString()}
                </td>
                <td className="px-4 py-3 text-right">
                  {b.balanceDue > 0 && (
                    <button
                      type="button"
                      onClick={() => selectBill(b)}
                      className="text-xs font-medium text-slate-600 underline"
                    >
                      Pay
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr>
                <td colSpan={8} className="px-4 py-8 text-center text-slate-500">
                  No supplier bills.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </AppShell>
  );
}
