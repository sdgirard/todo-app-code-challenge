import { isRouteErrorResponse, Link, useRouteError } from 'react-router'

export const ErrorBoundary = () => {
  const error = useRouteError()

  if (isRouteErrorResponse(error) && error.status === 404) {
    return (
      <main className="mx-auto max-w-md p-6">
        <p className="mb-4 text-sm text-gray-700">Todo not found</p>
        <Link to="/" className="text-sm text-gray-500 hover:underline">
          &larr; Back to list
        </Link>
      </main>
    )
  }

  return (
    <main className="mx-auto max-w-md p-6">
      <p className="text-sm text-gray-700">Something went wrong</p>
    </main>
  )
}
