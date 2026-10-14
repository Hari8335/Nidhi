import { AdminTransactionsPage } from "@/components/admin/admin-pages";
export default async function Page({ searchParams }: { searchParams: Promise<{ customerId?: string }> }) { const query = await searchParams; return <AdminTransactionsPage customerId={typeof query.customerId === "string" ? query.customerId : ""} />; }
