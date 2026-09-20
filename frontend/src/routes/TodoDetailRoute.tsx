import { Link, useLoaderData } from 'react-router'
import { TodoDetail } from '../components/TodoDetail'
import type { TodoResponse } from '../api/generated/models'

export const TodoDetailRoute = () => {
  const todo = useLoaderData() as TodoResponse

  return (
    <main className="mx-auto max-w-md p-6">
      <Link to="/" className="mb-4 inline-block text-sm text-gray-500 hover:underline">
        &larr; Back to list
      </Link>

      <TodoDetail todo={todo} />
    </main>
  )
}
