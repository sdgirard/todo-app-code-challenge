import { redirect, type ActionFunctionArgs } from 'react-router'
import { addTodo } from '../api/generated/todos/todos'
import { fromDateTimeLocal } from '../lib/dateTimeLocal'
import { getFieldErrors, getMessage } from '../lib/problemDetails'

export interface TodoListActionData {
  fieldErrors: Record<string, string[]>
  message?: string
}

export const todoListAction = async ({
  request,
}: ActionFunctionArgs): Promise<TodoListActionData | Response> => {
  const formData = await request.formData()

  const title = String(formData.get('title') ?? '')
  const description = formData.get('description')
  const dueDate = formData.get('dueDate')

  const result = await addTodo({
    title,
    description: description ? String(description) : null,
    dueDate: dueDate ? fromDateTimeLocal(String(dueDate)) : null,
  })

  if (result.status === 201) {
    return redirect('/')
  }

  return {
    fieldErrors: getFieldErrors(result.data),
    message: getMessage(result.data),
  }
}
