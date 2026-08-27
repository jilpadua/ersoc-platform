"use client";

import { useRouter } from "next/navigation";
import { AppShell } from "@/components/app-shell";
import { api, ApiClientError, Paged } from "@/lib/api";
import { FormEvent, useEffect, useState } from "react";

type Customer = { id: string; name: string };
type Part = { id: string; sku: string; name: string; unitPrice: number; quantityOnHand: number };
type PaymentMethod = { id: string; code: string; name: string };

type LineDraft = { partId: string; quantity: string; unitPrice: string };

const fieldClass = "w-full rounded-md border border-slate-300 px-3 py-2 text-sm";
const labelClass = "mb-1 block text-xs font-medium text-slate-600";

function newIdempotencyKey() {
  return crypto.randomUUID();
}

export default function NewSalePage() {
  const router = useRouter();
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [parts, setParts] = useState<Part[]>([]);
  const [methods, setMethods] = useState<PaymentMethod[]>([]);
  const [customerId, setCustomerId] = useState("");
  const [lines, setLines] = useState<LineDraft[]>([{ partId: "", quantity: "1", unitPrice: "" }]);
  const [payNow, setPayNow] = useState(true);
  const [payAmount, setPayAmount] = useState("");
  const [methodCode, setMethodCode] = useState("CASH");
  const [notes, setNotes] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    Promise.all([
      api<Paged<Customer>>("/api/v1/customers?pageSize=100"),
      api<Paged<Part>>("/api/v1/parts?pageSize=100"),
      api<PaymentMethod[]>("/api/v1/payment-methods"),
    ])
      .then(([c, p, m]) => {
        setCustomers(c.items);
        setParts(p.items);
        setMethods(m);
        if (m[0]) setMethodCode(m[0].code);
        if (p.items[0]) {
          setLines([{ partId: p.items[0].id, quantity: "1", unitPrice: String(p.items[0].unitPrice) }]);
        }
      })
      .catch((e) => setError(e.message));
  }, []);

  const lineTotal = lines.reduce((sum, l) => {
    const qty = Number(l.quantity || 0);
    const price = Number(l.unitPrice || 0);
    return sum + qty * price;
  }, 0);

  function updateLine(index: number, patch: Partial<LineDraft>) {
    setLines((prev) => prev.map((l, i) => (i === index ? { ...l, ...patch } : l)));
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSaving(true);
    try {
      const body = {
        customerId: customerId || null,
        notes: notes || null,
        lines: lines
          .filter((l) => l.partId)
          .map((l) => ({
            partId: l.partId,
            quantity: Number(l.quantity || 0),
            unitPrice: Number(l.unitPrice || 0),
          })),
        payment:
          payNow && Number(payAmount || lineTotal) > 0
            ? {
                amount: Number(payAmount || lineTotal),
                methodCode,
                idempotencyKey: newIdempotencyKey(),
              }
            : null,
      };
      const sale = await api<{ id: string }>("/api/v1/sales", {
        method: "POST",
        body: JSON.stringify(body),
      });
      router.push(`/sales/${sale.id}`);
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed to complete sale");
      setSaving(false);
    }
  }

  return (
    <AppShell>
      <h1 className="mb-4 text-2xl font-semibold">New sale</h1>
      <form onSubmit={onSubmit} className="space-y-4 rounded-lg border border-slate-200 bg-white p-4">
        <div className="grid gap-3 md:grid-cols-2">
          <div>
            <label className={labelClass}>Customer (optional)</label>
            <select value={customerId} onChange={(e) => setCustomerId(e.target.value)} className={fieldClass}>
              <option value="">Walk-in</option>
              {customers.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className={labelClass}>Notes</label>
            <input value={notes} onChange={(e) => setNotes(e.target.value)} className={fieldClass} />
          </div>
        </div>

        <div className="space-y-3">
          <div className="text-sm font-semibold text-slate-700">Lines</div>
          {lines.map((line, index) => (
            <div key={index} className="grid gap-3 md:grid-cols-4">
              <div className="md:col-span-2">
                <label className={labelClass}>Part</label>
                <select
                  value={line.partId}
                  onChange={(e) => {
                    const part = parts.find((p) => p.id === e.target.value);
                    updateLine(index, {
                      partId: e.target.value,
                      unitPrice: part ? String(part.unitPrice) : "",
                    });
                  }}
                  required
                  className={fieldClass}
                >
                  <option value="">Select part</option>
                  {parts.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.sku} — {p.name} (on hand {p.quantityOnHand})
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
                  value={line.quantity}
                  onChange={(e) => updateLine(index, { quantity: e.target.value })}
                  required
                  className={fieldClass}
                />
              </div>
              <div>
                <label className={labelClass}>Unit price</label>
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  value={line.unitPrice}
                  onChange={(e) => updateLine(index, { unitPrice: e.target.value })}
                  required
                  className={fieldClass}
                />
              </div>
            </div>
          ))}
          <button
            type="button"
            onClick={() =>
              setLines((prev) => [
                ...prev,
                {
                  partId: parts[0]?.id ?? "",
                  quantity: "1",
                  unitPrice: parts[0] ? String(parts[0].unitPrice) : "",
                },
              ])
            }
            className="rounded-md border border-slate-300 px-3 py-1.5 text-sm"
          >
            Add line
          </button>
        </div>

        <div className="border-t border-slate-100 pt-4">
          <div className="mb-3 font-mono text-lg">Total ₱{lineTotal.toLocaleString()}</div>
          <label className="mb-3 flex items-center gap-2 text-sm">
            <input type="checkbox" checked={payNow} onChange={(e) => setPayNow(e.target.checked)} />
            Pay now
          </label>
          {payNow && (
            <div className="grid gap-3 md:grid-cols-2">
              <div>
                <label className={labelClass}>Payment amount</label>
                <input
                  type="number"
                  step="0.01"
                  min="0.01"
                  value={payAmount}
                  onChange={(e) => setPayAmount(e.target.value)}
                  placeholder={String(lineTotal)}
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
            </div>
          )}
        </div>

        {error && <p className="text-sm text-red-600">{error}</p>}
        <button
          type="submit"
          disabled={saving}
          className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
        >
          {saving ? "Completing…" : "Complete sale"}
        </button>
      </form>
    </AppShell>
  );
}
