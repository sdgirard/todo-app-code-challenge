import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { TodoList } from '../../src/components/TodoList'
import type { TodoResponse } from '../../src/api/generated/models'

const todos: TodoResponse[] = [
  {
    id: 'id-1',
    title: 'Buy milk',
    description: null,
    dueDate: '2026-09-25T00:00:00Z',
    isCompleted: false,
    createdAt: '2026-09-19T14:32:07Z',
    updatedAt: null,
  },
  {
    id: 'id-2',
    title: 'Finish report',
    description: null,
    dueDate: null,
    isCompleted: true,
    createdAt: '2026-09-19T14:32:07Z',
    updatedAt: null,
  },
]

const renderList = (list: TodoResponse[]) => {
  const router = createMemoryRouter(
    [{ path: '/', element: <TodoList todos={list} /> }],
    { initialEntries: ['/'] },
  )
  render(<RouterProvider router={router} />)
}

describe('TodoList', () => {
  it('renders title, due date, and completion status for each item', () => {
    renderList(todos)

    expect(screen.getByText('Buy milk')).toBeInTheDocument()
    expect(screen.getByText('Incomplete')).toBeInTheDocument()
    expect(screen.getByText('Finish report')).toBeInTheDocument()
    expect(screen.getByText('Completed')).toBeInTheDocument()
    expect(screen.getByText('No due date')).toBeInTheDocument()
  })

  it('renders a due date including its time, not just the date', () => {
    renderList(todos)

    const expected = new Date(todos[0]!.dueDate!).toLocaleString()
    expect(screen.getByText(expected)).toBeInTheDocument()
  })

  it('links each row to its detail route', () => {
    renderList(todos)

    expect(screen.getByRole('link', { name: 'Buy milk' })).toHaveAttribute(
      'href',
      '/todos/id-1',
    )
    expect(screen.getByRole('link', { name: 'Finish report' })).toHaveAttribute(
      'href',
      '/todos/id-2',
    )
  })

  it('renders an empty-state message for an empty list', () => {
    renderList([])

    expect(screen.getByText('No to-dos yet.')).toBeInTheDocument()
  })
})
