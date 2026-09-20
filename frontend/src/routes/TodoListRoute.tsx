import { useActionData, useLoaderData, useNavigation } from 'react-router'
import { TodoForm } from '../components/TodoForm'
import { TodoList } from '../components/TodoList'
import type { TodoResponse } from '../api/generated/models'
import type { TodoListActionData } from './TodoListRoute.action'

export const TodoListRoute = () => {
  const todos = useLoaderData() as TodoResponse[]
  const actionData = useActionData() as TodoListActionData | undefined
  const navigation = useNavigation()

  const submitting =
    navigation.state === 'submitting' && navigation.formMethod?.toLowerCase() === 'post'

  return (
    <main className="mx-auto max-w-md p-6">
      <h1 className="mb-4 text-2xl font-semibold text-gray-900">To-Do List</h1>

      {actionData?.message && (
        <p className="mb-4 text-sm text-red-600">{actionData.message}</p>
      )}

      <TodoForm
        intent="add"
        fieldErrors={actionData?.fieldErrors}
        submitting={submitting}
      />

      <TodoList todos={todos} />
    </main>
  )
}
