"use client";

import { AppShell } from "@/components/app-shell";
import { useAuth } from "@/components/auth-provider";
import { api, ApiClientError, Paged } from "@/lib/api";
import { formatOrgDateTime } from "@/lib/datetime";
import { FormEvent, useEffect, useState } from "react";

type Category = {
  id: string;
  name: string;
  accountId: string;
  accountCode: string;
  accountName: string;
  isActive: boolean;
};

type Expense = {
  id: string;
  categoryId: string;
  categoryName: string;
  amount: number;
  expenseDate: string;
  payee?: string | null;
  methodCode?: string | null;
  payable: boolean;
  status: string;
  notes?: string | null;
  approvedAt?: string | null;
  postedAt?: string | null;
};

const fieldClass = "w-full rounded-md border border-slate-300 px-3 py-2 text-sm";
const labelClass = "mb-1 block text-xs font-medium text-slate-600";

export default function ExpensesPage() {
  const { user } = useAuth();
  const tz = user?.timeZoneId;
  const [items, setItems] = useState<Expense[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [status, setStatus] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [amount, setAmount] = useState("");
  const [expenseDate, setExpenseDate] = useState(
    () => new Date().toISOString().slice(0, 16)
  );
  const [payee, setPayee] = useState("");
  const [methodCode, setMethodCode] = useState("CASH");
  const [payable, setPayable] = useState(false);
  const [notes, setNotes] = useState("");
  const [error, setError] = useState<string | null>(null);

  async function load(st = status) {
    const qs = ["pageSize=50", st ? `status=${encodeURIComponent(st)}` : ""]
      .filter(Boolean)
      .join("&");
    const [exps, cats] = await Promise.all([
      api<Paged<Expense>>(`/api/v1/expenses?${qs}`),
      api<Category[]>("/api/v1/expenses/categories?activeOnly=true"),
    ]);
    setItems(exps.items);
    setCategories(cats);
    if (!categoryId && cats[0]) setCategoryId(cats[0].id);
  }

  useEffect(() => {
    load().catch((e) => setError(e.message));
  }, []);

  async function onCreate(e: FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await api("/api/v1/expenses", {
        method: "POST",
        body: JSON.stringify({
          categoryId,
          amount: Number(amount),
          expenseDate: new Date(expenseDate).toISOString(),
          payee: payee || null,
          methodCode: methodCode || null,
          payable,
          notes: notes || null,
        }),
      });
      setAmount("");
      setPayee("");
      setNotes("");
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  async function approve(id: string) {
    setError(null);
    try {
      await api(`/api/v1/expenses/${id}/approve`, {
        method: "POST",
        body: JSON.stringify({}),
      });
      await load();
    } catch (err: unknown) {
      setError(err instanceof ApiClientError ? err.message : "Failed");
    }
  }

  return (
    <AppShell>
      <h1 className="mb-4 text-2xl font-semibold">Expenses</h1>
      <form
        onSubmit={onCreate}
        className="mb-4 grid gap-3 rounded-lg border border-slate-200 bg-white p-4 md:grid-cols-3"
      >
        <div>
          <label className={labelClass}>Category</label>
          <select
            value={categoryId}
            onChange={(e) => setCategoryId(e.target.value)}
            required
            className={fieldClass}
          >
            <option value="">Select category</option>
            {categories.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
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
          <label className={labelClass}>Expense date</label>
          <input
            type="datetime-local"
            value={expenseDate}
            onChange={(e) => setExpenseDate(e.target.value)}
            required
            className={fieldClass}
          />
        </div>
        <div>
          <label className={labelClass}>Payee</label>
          <input
            value={payee}
            onChange={(e) => setPayee(e.target.value)}
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
            <option value="CASH">Cash</option>
            <option value="CARD">Card</option>
            <option value="TRANSFER">Transfer</option>
          </select>
        </div>
        <div className="flex items-end gap-3">
          <label className="flex items-center gap-2 pb-2 text-sm text-slate-600">
            <input
              type="checkbox"
              checked={payable}
              onChange={(e) => setPayable(e.target.checked)}
            />
            Payable
          </label>
        </div>
        <div className="md:col-span-2">
          <label className={labelClass}>Notes</label>
          <input
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            className={fieldClass}
          />
        </div>
        <div className="flex items-end">
          <button className="rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white">
            Create draft
          </button>
        </div>
      </form>
      <div className="mb-3">
        <select
          value={status}
          onChange={(e) => {
            const v = e.target.value;
            setStatus(v);
            load(v).catch((err) => setError(err.message));
          }}
          className="rounded-md border border-slate-300 px-3 py-2 text-sm"
        >
          <option value="">All statuses</option>
          <option value="Draft">Draft</option>
          <option value="Approved">Approved</option>
          <option value="Posted">Posted</option>
          <option value="Voided">Voided</option>
        </select>
      </div>
      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">Date</th>
              <th className="px-4 py-3">Category</th>
              <th className="px-4 py-3">Payee</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3 text-right">Amount</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {items.map((x) => (
              <tr key={x.id} className="border-b border-slate-100">
                <td className="px-4 py-3 font-mono text-xs">
                  {formatOrgDateTime(x.expenseDate, tz)}
                </td>
                <td className="px-4 py-3">{x.categoryName}</td>
                <td className="px-4 py-3">{x.payee ?? "—"}</td>
                <td className="px-4 py-3">{x.status}</td>
                <td className="px-4 py-3 text-right font-mono">
                  ₱{x.amount.toLocaleString()}
                </td>
                <td className="px-4 py-3 text-right">
                  {x.status === "Draft" && (
                    <button
                      type="button"
                      onClick={() => void approve(x.id)}
                      className="text-xs font-medium text-slate-600 underline"
                    >
                      Approve
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr>
                <td colSpan={6} className="px-4 py-8 text-center text-slate-500">
                  No expenses.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </AppShell>
  );
}
