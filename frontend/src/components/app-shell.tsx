"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/components/auth-provider";
import { FormEvent, useEffect, useState } from "react";
import { api } from "@/lib/api";

// ---------------------------------------------------------------------------
// Navigation structure
// ---------------------------------------------------------------------------

type NavItem = { href: string; label: string };
type NavGroup = { label?: string; items: NavItem[] };

const navGroups: NavGroup[] = [
  {
    items: [{ href: "/dashboard", label: "Dashboard" }],
  },
  {
    label: "OPERATIONS",
    items: [
      { href: "/customers", label: "Customers" },
      { href: "/devices", label: "Devices" },
      { href: "/repairs", label: "Repairs" },
      { href: "/services", label: "Service catalog" },
    ],
  },
  {
    label: "INVENTORY",
    items: [
      { href: "/parts", label: "Parts" },
      { href: "/suppliers", label: "Suppliers" },
      { href: "/purchase-orders", label: "Purchase orders" },
    ],
  },
  {
    label: "SALES & FINANCE",
    items: [
      { href: "/sales", label: "Sales" },
      { href: "/invoices", label: "Invoices" },
    ],
  },
  {
    label: "ADMINISTRATION",
    items: [
      { href: "/audit", label: "Audit log" },
      { href: "/settings", label: "Settings" },
    ],
  },
];

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function AppShell({ children }: { children: React.ReactNode }) {
  const { user, logout, loading } = useAuth();
  const pathname = usePathname();
  const router = useRouter();
  const [q, setQ] = useState("");
  const [results, setResults] = useState<
    { type: string; id: string; title: string; subtitle?: string }[]
  >([]);
  const [sidebarOpen, setSidebarOpen] = useState(false);

  useEffect(() => {
    if (!loading && !user) {
      router.replace("/login");
    }
  }, [loading, user, router]);

  if (loading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-100 text-slate-600">
        Loading...
      </div>
    );
  }

  if (!user) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-100 text-slate-600">
        Redirecting...
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

  function closeSidebar() {
    setSidebarOpen(false);
  }

  return (
    <div className="min-h-screen bg-slate-100 text-slate-900">
      <div className="mx-auto flex min-h-screen max-w-[1400px]">

        {/* Mobile overlay */}
        {sidebarOpen && (
          <div
            className="fixed inset-0 z-20 bg-black/30 md:hidden"
            aria-hidden="true"
            onClick={closeSidebar}
          />
        )}

        {/* Sidebar */}
        <aside
          id="main-nav-sidebar"
          className={[
            "fixed inset-y-0 left-0 z-30 flex w-56 flex-col border-r border-slate-200 bg-white px-3 py-5",
            "transition-transform duration-200",
            "md:static md:translate-x-0",
            sidebarOpen ? "translate-x-0" : "-translate-x-full",
          ].join(" ")}
        >
          {/* Brand */}
          <div className="mb-6 px-2">
            <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">
              ERSMS
            </div>
            <div className="text-lg font-semibold text-slate-900">
              Repair Ops
            </div>
          </div>

          {/* Nav groups */}
          <nav
            aria-label="Main navigation"
            className="flex flex-1 flex-col overflow-y-auto"
          >
            {navGroups.map((group, gi) => (
              <div key={gi} className={gi > 0 ? "mt-4" : undefined}>
                {/* Section label - decorative, hidden from screen readers */}
                {group.label && (
                  <div
                    aria-hidden="true"
                    className="mb-1 px-3 text-[10px] font-semibold uppercase tracking-widest text-slate-400"
                  >
                    {group.label}
                  </div>
                )}

                {/* Links */}
                <div className="flex flex-col gap-0.5">
                  {group.items.map((item) => {
                    const active = pathname.startsWith(item.href);
                    return (
                      <Link
                        key={item.href}
                        href={item.href}
                        aria-current={active ? "page" : undefined}
                        onClick={closeSidebar}
                        className={[
                          "rounded-md px-3 py-2.5 text-sm font-medium",
                          "focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-slate-500",
                          active
                            ? "bg-slate-900 text-white"
                            : "text-slate-700 hover:bg-slate-100",
                        ].join(" ")}
                      >
                        {item.label}
                      </Link>
                    );
                  })}
                </div>
              </div>
            ))}
          </nav>

          {/* User footer */}
          <div className="mt-4 border-t border-slate-200 px-2 pt-4 text-sm">
            <div className="font-medium">{user.displayName}</div>
            <div className="text-xs text-slate-500">{user.email}</div>
            <button
              type="button"
              onClick={() => void logout().then(() => router.push("/login"))}
              className="mt-3 text-xs font-medium text-slate-600 underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-slate-500"
            >
              Sign out
            </button>
          </div>
        </aside>

        {/* Main content */}
        <main className="flex-1 p-6">
          {/* Mobile menu button - hidden on md+ */}
          <button
            type="button"
            aria-label="Open navigation menu"
            aria-expanded={sidebarOpen}
            aria-controls="main-nav-sidebar"
            onClick={() => setSidebarOpen((o) => !o)}
            className="mb-4 flex items-center gap-2 rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-slate-500 md:hidden"
          >
            <svg
              width="16"
              height="16"
              viewBox="0 0 16 16"
              fill="none"
              aria-hidden="true"
            >
              <rect y="2" width="16" height="1.5" rx="0.75" fill="currentColor" />
              <rect y="7.25" width="16" height="1.5" rx="0.75" fill="currentColor" />
              <rect y="12.5" width="16" height="1.5" rx="0.75" fill="currentColor" />
            </svg>
            Menu
          </button>

          {/* Search */}
          <form onSubmit={onSearch} className="mb-4 flex gap-2">
            <input
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder="Search repairs, customers, devices..."
              className="w-full max-w-xl rounded-md border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-slate-500"
            />
            <button
              type="submit"
              className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-slate-500"
            >
              Search
            </button>
          </form>

          {/* Search results */}
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
                  className="block rounded px-2 py-1.5 hover:bg-slate-50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-slate-500"
                  onClick={() => setResults([])}
                >
                  <span className="mr-2 rounded bg-slate-100 px-1.5 py-0.5 text-xs uppercase text-slate-600">
                    {r.type}
                  </span>
                  {r.title}
                  {r.subtitle ? (
                    <span className="text-slate-500"> -- {r.subtitle}</span>
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
