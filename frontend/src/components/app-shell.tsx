"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/components/auth-provider";
import { FormEvent, useEffect, useRef, useState } from "react";
import { api } from "@/lib/api";

type NavItem = { href: string; label: string };
type NavGroup = { id: string; label: string; items: NavItem[] };

const dashboardItem: NavItem = { href: "/dashboard", label: "Dashboard" };

const navGroups: NavGroup[] = [
  {
    id: "operations",
    label: "Operations",
    items: [
      { href: "/customers", label: "Customers" },
      { href: "/devices", label: "Devices" },
      { href: "/repairs", label: "Repairs" },
    ],
  },
  {
    id: "catalog",
    label: "Catalog",
    items: [
      { href: "/services", label: "Services" },
      { href: "/parts", label: "Parts" },
    ],
  },
  {
    id: "purchasing",
    label: "Purchasing",
    items: [
      { href: "/suppliers", label: "Suppliers" },
      { href: "/purchase-orders", label: "Purchase orders" },
      { href: "/accounting/supplier-bills", label: "Supplier bills" },
    ],
  },
  {
    id: "sales",
    label: "Sales",
    items: [
      { href: "/sales", label: "Sales" },
      { href: "/invoices", label: "Invoices" },
    ],
  },
  {
    id: "accounting",
    label: "Accounting",
    items: [
      { href: "/accounting/accounts", label: "Accounts" },
      { href: "/accounting/journals", label: "Journals" },
      { href: "/accounting/expenses", label: "Expenses" },
      { href: "/accounting/reconciliation", label: "Reconciliation" },
      { href: "/accounting/periods", label: "Periods" },
      { href: "/accounting/mappings", label: "Mappings" },
    ],
  },
  {
    id: "insights",
    label: "Insights",
    items: [
      { href: "/accounting/reports", label: "Reports" },
      { href: "/audit", label: "Audit" },
    ],
  },
  {
    id: "system",
    label: "System",
    items: [{ href: "/settings", label: "Settings" }],
  },
];

const STORAGE_KEY = "ersms.sidebar.sections";

function defaultExpanded(): Record<string, boolean> {
  return Object.fromEntries(navGroups.map((g) => [g.id, true]));
}

function isNavActive(pathname: string, href: string) {
  return pathname === href || pathname.startsWith(`${href}/`);
}

function loadExpanded(): Record<string, boolean> {
  const defaults = defaultExpanded();
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return defaults;
    const parsed = JSON.parse(raw) as Record<string, boolean>;
    return { ...defaults, ...parsed };
  } catch {
    return defaults;
  }
}

function linkClass(active: boolean) {
  return `rounded-md px-3 py-2 text-sm font-medium ${
    active ? "bg-slate-900 text-white" : "text-slate-700 hover:bg-slate-100"
  }`;
}

export function AppShell({ children }: { children: React.ReactNode }) {
  const { user, logout, loading } = useAuth();
  const pathname = usePathname();
  const router = useRouter();
  const [q, setQ] = useState("");
  const [results, setResults] = useState<
    { type: string; id: string; title: string; subtitle?: string }[]
  >([]);
  const [expanded, setExpanded] = useState<Record<string, boolean>>(defaultExpanded);
  const prevPathnameRef = useRef<string | null>(null);
  const sectionsHydratedRef = useRef(false);

  useEffect(() => {
    if (!loading && !user) {
      router.replace("/login");
    }
  }, [loading, user, router]);

  useEffect(() => {
    function openGroupsForPath(
      path: string,
      base: Record<string, boolean>
    ): Record<string, boolean> {
      const next = { ...base };
      for (const group of navGroups) {
        if (group.items.some((item) => isNavActive(path, item.href))) {
          next[group.id] = true;
        }
      }
      return next;
    }

    if (!sectionsHydratedRef.current) {
      sectionsHydratedRef.current = true;
      setExpanded(openGroupsForPath(pathname, loadExpanded()));
      prevPathnameRef.current = pathname;
      return;
    }

    if (prevPathnameRef.current === pathname) return;
    prevPathnameRef.current = pathname;

    setExpanded((prev) => {
      const next = openGroupsForPath(pathname, prev);
      return next;
    });
  }, [pathname]);

  function isGroupExpanded(groupId: string) {
    return expanded[groupId] !== false;
  }

  function toggleGroup(groupId: string) {
    setExpanded((prev) => {
      const currentlyOpen = prev[groupId] !== false;
      const next = { ...prev, [groupId]: !currentlyOpen };
      try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
      } catch {
        /* ignore quota / private mode */
      }
      return next;
    });
  }

  if (loading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-100 text-slate-600">
        Loading…
      </div>
    );
  }

  if (!user) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-100 text-slate-600">
        Redirecting…
      </div>
    );
  }

  async function onSearch(e: FormEvent) {
    e.preventDefault();
    if (q.trim().length < 2) {
      setResults([]);
      return;
    }
    const data = await api<typeof results>(
      `/api/v1/search?q=${encodeURIComponent(q.trim())}`
    );
    setResults(data);
  }

  return (
    <div className="min-h-screen bg-slate-100 text-slate-900">
      <div className="mx-auto flex min-h-screen max-w-[1400px]">
        <aside className="flex w-56 shrink-0 flex-col overflow-y-auto border-r border-slate-200 bg-white px-3 py-5">
          <div className="mb-6 px-2">
            <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">
              ERSMS
            </div>
            <div className="text-lg font-semibold text-slate-900">
              Repair Ops
            </div>
          </div>
          <nav className="flex flex-1 flex-col gap-1">
            <Link
              href={dashboardItem.href}
              className={linkClass(isNavActive(pathname, dashboardItem.href))}
            >
              {dashboardItem.label}
            </Link>

            {navGroups.map((group) => {
              const open = isGroupExpanded(group.id);
              const panelId = `nav-section-${group.id}`;
              return (
                <div key={group.id} className="mt-3">
                  <button
                    type="button"
                    aria-expanded={open}
                    aria-controls={panelId}
                    onClick={() => toggleGroup(group.id)}
                    className="flex w-full items-center justify-between rounded-md px-2 py-1.5 text-left text-xs font-semibold uppercase tracking-wide text-slate-500 outline-none hover:text-slate-700 focus-visible:ring-2 focus-visible:ring-slate-400"
                  >
                    <span>{group.label}</span>
                    <span className="text-[10px] leading-none" aria-hidden>
                      {open ? "˅" : "˃"}
                    </span>
                  </button>
                  {open && (
                    <div id={panelId} className="mt-1 flex flex-col gap-1 pl-1">
                      {group.items.map((item) => {
                        const active = isNavActive(pathname, item.href);
                        return (
                          <Link
                            key={item.href}
                            href={item.href}
                            className={linkClass(active)}
                          >
                            {item.label}
                          </Link>
                        );
                      })}
                    </div>
                  )}
                </div>
              );
            })}
          </nav>
          <div className="mt-4 border-t border-slate-200 px-2 pt-4 text-sm">
            <div className="font-medium">{user.displayName}</div>
            <div className="text-xs text-slate-500">{user.email}</div>
            <button
              type="button"
              onClick={() => void logout().then(() => router.push("/login"))}
              className="mt-3 text-xs font-medium text-slate-600 underline"
            >
              Sign out
            </button>
          </div>
        </aside>
        <main className="min-w-0 flex-1 p-6">
          <form onSubmit={onSearch} className="mb-4 flex gap-2">
            <input
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder="Search repairs, customers, devices…"
              className="w-full max-w-xl rounded-md border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-slate-500"
            />
            <button
              type="submit"
              className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white"
            >
              Search
            </button>
          </form>
          {results.length > 0 && (
            <div className="mb-4 rounded-md border border-slate-200 bg-white p-3 text-sm">
              {results.map((r) => (
                <Link
                  key={`${r.type}-${r.id}`}
                  href={
                    r.type === "repair"
                      ? `/repairs/${r.id}`
                      : r.type === "customer"
                        ? `/customers/${r.id}`
                        : `/devices/${r.id}`
                  }
                  className="block rounded px-2 py-1.5 hover:bg-slate-50"
                  onClick={() => setResults([])}
                >
                  <span className="mr-2 rounded bg-slate-100 px-1.5 py-0.5 text-xs uppercase text-slate-600">
                    {r.type}
                  </span>
                  {r.title}
                  {r.subtitle ? (
                    <span className="text-slate-500"> — {r.subtitle}</span>
                  ) : null}
                </Link>
              ))}
            </div>
          )}
          {children}
        </main>
      </div>
    </div>
  );
}
