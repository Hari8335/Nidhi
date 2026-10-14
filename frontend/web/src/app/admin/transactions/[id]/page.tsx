import { AdminTransactionPage } from "@/components/admin/admin-pages";
export default async function Page({ params }: { params: Promise<{ id: string }> }) { const { id } = await params; return <AdminTransactionPage id={id} />; }
