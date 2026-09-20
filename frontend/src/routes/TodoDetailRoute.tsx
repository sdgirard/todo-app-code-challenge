import { useState } from 'react'
import { Link, useLoaderData, useNavigation, useSubmit } from 'react-router'
import { TodoDetail } from '../components/TodoDetail'
import { ConfirmDialog } from '../components/ConfirmDialog'
import type { TodoResponse } from '../api/generated/models'

export const TodoDetailRoute = () => {
  const todo = useLoaderData() as TodoResponse
  const navigation = useNavigation()
  const submit = useSubmit()
  const [showConfirm, setShowConfirm] = useState(false)

  const deleting =
    navigation.state === 'submitting' && navigation.formMethod?.toLowerCase() === 'post'

  const handleConfirmDelete = () => {
    const formData = new FormData()
    formData.set('intent', 'delete')
    submit(formData, { method: 'post' })
  }

  return (
    <main className="mx-auto max-w-md p-6">
      <Link to="/" className="mb-4 inline-block text-sm text-gray-500 hover:underline">
        &larr; Back to list
      </Link>

      <TodoDetail todo={todo} />

      <button
        type="button"
        onClick={() => setShowConfirm(true)}
        disabled={deleting}
        className="mt-4 rounded bg-red-600 px-3 py-1 text-sm text-white disabled:opacity-50"
      >
        Delete
      </button>

      <ConfirmDialog
        open={showConfirm}
        message="Delete this to-do?"
        onConfirm={handleConfirmDelete}
        onCancel={() => setShowConfirm(false)}
        disabled={deleting}
      />
    </main>
  )
}
